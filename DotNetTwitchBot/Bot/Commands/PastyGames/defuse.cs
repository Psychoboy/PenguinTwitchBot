using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class Defuse : BaseCommand
    {
        public List<string> Wires = new List<string> { "red", "blue", "yellow" };
        public int Cost = 500;
        public int Cooldown = 10;
        private LoyaltyFeature _loyaltyFeature;
        private ILogger<Defuse> _logger;
        private SendAlerts _sendAlerts;

        public Defuse(
            LoyaltyFeature loyaltyFeature,
            ServiceBackbone serviceBackbone,
            SendAlerts sendAlerts,
            ILogger<Defuse> logger
            ) : base(serviceBackbone)
        {
            _loyaltyFeature = loyaltyFeature;
            _logger = logger;
            _sendAlerts = sendAlerts;
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = "testdefuse";
            if (!e.Command.Equals(command)) return;
            if (!IsCoolDownExpired(e.Name, command)) return;

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

            var chosenWire = Tools.RandomElement(Wires);
            if (chosenWire == null)
            {
                _logger.LogError("Couldn't choose a wire for defuse");
                return;
            }
            var startMessage = string.Format("The bomb is beeping and {0} cuts the {1} wire... ", e.DisplayName, e.Arg);
            if (chosenWire.Equals(e.Arg, StringComparison.CurrentCultureIgnoreCase))
            {
                var multiplier = 3;
                var min = Cost * multiplier - Cost / multiplier;
                var max = Cost * multiplier + Cost / multiplier;
                var value = Tools.CurrentThreadRandom.Next(min, max);
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
    }
}