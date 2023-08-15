using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class Steal : BaseCommandService
    {
        private readonly int StealMin = 100;
        private readonly int StealMax = 10000;
        private readonly LoyaltyFeature _loyaltyFeature;
        private readonly ViewerFeature _viewerFeature;
        private readonly ILogger<Steal> _logger;

        public Steal(
            ILogger<Steal> logger,
            LoyaltyFeature loyaltyFeature,
            ViewerFeature viewerFeature,
            IServiceBackbone serviceBackbone,
            ICommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler)
        {
            _loyaltyFeature = loyaltyFeature;
            _viewerFeature = viewerFeature;
            _logger = logger;
        }

        public override async Task Register()
        {
            var moduleName = "Steal";
            await RegisterDefaultCommand("steal", this, moduleName, userCooldown: 300);
            _logger.LogInformation($"Registered commands for {moduleName}");
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            if (!command.CommandProperties.CommandName.Equals("steal")) return;
            if (string.IsNullOrWhiteSpace(e.TargetUser) || e.Name.Equals(e.TargetUser))
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, "to steal from someone the command is !steal TARGETNAME");
                throw new SkipCooldownException();
            }

            if (!ServiceBackbone.IsOnline)
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, "you can't steal from someone when the stream is offline");
                throw new SkipCooldownException();
            }

            var userPasties = await _loyaltyFeature.GetUserPasties(e.Name);
            if (userPasties.Points < StealMax)
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, string.Format("you don't have enough pasties to steal, you need a minimum of {0}", StealMax));
                throw new SkipCooldownException();
            }

            await StealFromUser(e);
        }

        private async Task StealFromUser(CommandEventArgs e)
        {
            var targetPasties = await _loyaltyFeature.GetUserPasties(e.TargetUser);
            var targetDisplayName = await _viewerFeature.GetNameWithTitle(e.TargetUser);
            var amount = Tools.Next(StealMin, StealMax + 1);
            if (targetPasties.Points < StealMax)
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName,
                string.Format("{0} is to poor for you to steal from them, instead you give them {1} pasties.", targetDisplayName, amount.ToString("N0")));
                await MovePoints(e.Name, e.TargetUser, amount);
                return;
            }
            var rand = Tools.Next(1, 12 + 1);
            if (rand <= 4) // success
            {
                await ServiceBackbone.SendChatMessage(string.Format("{0} successfully stole {1} pasties from {2}",
                e.DisplayName, amount, targetDisplayName));

                await MovePoints(e.TargetUser, e.Name, amount);
            }
            else
            {
                await ServiceBackbone.SendChatMessage(string.Format("{0} failed to steal {1} pasties from {2}, {2} gets {1} pasties from {0} instead",
               e.DisplayName, amount, targetDisplayName));
                await MovePoints(e.Name, e.TargetUser, amount);
            }
        }

        private async Task MovePoints(string from, string to, int amount)
        {
            await _loyaltyFeature.RemovePointsFromUser(from, amount);
            await _loyaltyFeature.AddPointsToViewer(to, amount);
        }
    }
}