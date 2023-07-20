using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class Defuse : BaseCommandService
    {
        public List<string> Wires = new List<string> { "red", "blue", "yellow" };
        public int Cost = 500;
        public int Cooldown = 10;
        private LoyaltyFeature _loyaltyFeature;
        private ILogger<Defuse> _logger;
        private SendAlerts _sendAlerts;
        private ViewerFeature _viewerFeature;

        public Defuse(
            LoyaltyFeature loyaltyFeature,
            ServiceBackbone serviceBackbone,
            ViewerFeature viewerFeature,
            SendAlerts sendAlerts,
            ILogger<Defuse> logger,
            IServiceScopeFactory scopeFactory,
            CommandHandler commandHandler
            ) : base(serviceBackbone, scopeFactory, commandHandler)
        {
            _loyaltyFeature = loyaltyFeature;
            _logger = logger;
            _sendAlerts = sendAlerts;
            _viewerFeature = viewerFeature;
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = "defuse";
            if (!e.Command.Equals(command)) return;
            var isCoolDownExpired = await IsCoolDownExpiredWithMessage(e.Name, e.DisplayName, e.Command);
            if (isCoolDownExpired == false) return;

            if (string.IsNullOrEmpty(e.Arg) ||
            (!e.Arg.Equals("red", StringComparison.CurrentCultureIgnoreCase) &&
            !e.Arg.Equals("blue", StringComparison.CurrentCultureIgnoreCase) &&
            !e.Arg.Equals("yellow", StringComparison.CurrentCultureIgnoreCase)))
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, string.Format("you need to choose one of these wires to cut: {0}", string.Join(", ", Wires)));
                return;
            }

            if (!(await _loyaltyFeature.RemovePointsFromUser(e.Name, Cost)))
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, string.Format("Sorry it costs {0} to defuse the bomb which you do not have.", Cost));
                return;
            }

            var chosenWire = Wires.RandomElement();
            if (chosenWire == null)
            {
                _logger.LogError("Couldn't choose a wire for defuse");
                return;
            }
            var startMessage = string.Format("The bomb is beeping and {0} cuts the {1} wire... ", await _viewerFeature.GetNameWithTitle(e.Name), e.Arg);
            if (chosenWire.Equals(e.Arg, StringComparison.CurrentCultureIgnoreCase))
            {
                var multiplier = 3;
                var min = Cost * multiplier - Cost / multiplier;
                var max = Cost * multiplier + Cost / multiplier;
                var value = Tools.Next(min, max + 1);
                await _loyaltyFeature.AddPointsToViewer(e.Name, value);
                await _serviceBackbone.SendChatMessage(startMessage + string.Format("The bomb goes silent. As a thank for saving the day you got awarded {0} pasties", value));
                _sendAlerts.QueueAlert("defuse.gif,8");
            }
            else
            {
                await _serviceBackbone.SendChatMessage(startMessage + string.Format("BOOM!!! The bomb explodes, you lose {0} pasties.", Cost));
                _sendAlerts.QueueAlert("detonated.gif,10");
            }
            AddCoolDown(e.Name, command, 10);
        }

        public override void RegisterDefaultCommands()
        {
            throw new NotImplementedException();
        }
    }
}