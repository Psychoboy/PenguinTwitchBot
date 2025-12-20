using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events.Chat;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class Steal(
        ILogger<Steal> logger,
        IPointsSystem pointsSystem,
        IViewerFeature viewerFeature,
        IServiceBackbone serviceBackbone,
        IMediator mediator,
        ICommandHandler commandHandler
            ) : BaseCommandService(serviceBackbone, commandHandler, "Steal", mediator), IHostedService
    {
        private readonly int StealMin = 100;
        private readonly int StealMax = 10000;

        public override async Task Register()
        {
            var moduleName = "Steal";
            await RegisterDefaultCommand("steal", this, moduleName, userCooldown: 300);
            await pointsSystem.RegisterDefaultPointForGame(ModuleName);
            logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            if (!command.CommandProperties.CommandName.Equals("steal")) return;
            if (string.IsNullOrWhiteSpace(e.TargetUser) || e.Name.Equals(e.TargetUser))
            {
                await ServiceBackbone.ResponseWithMessage(e, "to steal from someone the command is !steal TARGETNAME");
                throw new SkipCooldownException();
            }

            if (!ServiceBackbone.IsOnline)
            {
                await ServiceBackbone.ResponseWithMessage(e, "you can't steal from someone when the stream is offline");
                throw new SkipCooldownException();
            }

            var userPasties = await pointsSystem.GetUserPointsByUserIdAndGame(e.UserId, ModuleName);
            if (userPasties.Points < StealMax)
            {
                await ServiceBackbone.ResponseWithMessage(e, string.Format("you don't have enough pasties to steal, you need a minimum of {0}", StealMax));
                throw new SkipCooldownException();
            }

            await StealFromUser(e);
        }

        private async Task StealFromUser(CommandEventArgs e)
        {
            var targetPasties = await pointsSystem.GetUserPointsByUsernameAndGame(e.TargetUser, ModuleName);
            var targetDisplayName = await viewerFeature.GetNameWithTitle(e.TargetUser);
            var amount = StaticTools.Next(StealMin, StealMax + 1);
            if (targetPasties.Points < StealMax)
            {
                await ServiceBackbone.ResponseWithMessage(e,
                string.Format("{0} is to poor for you to steal from them, instead you give them {1} pasties.", targetDisplayName, amount.ToString("N0")));
                await MovePoints(e.Name, e.TargetUser, amount);
                return;
            }
            var rand = StaticTools.Next(1, 12 + 1);
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
            await pointsSystem.RemovePointsFromUserByUsernameAndGame(from, ModuleName, amount);
            await pointsSystem.RemovePointsFromUserByUsernameAndGame(to, ModuleName, amount);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Started {moduledname}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}