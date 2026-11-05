using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Bot.Commands.PastyGames
{
    public class Steal(
        ILogger<Steal> logger,
        IPointsSystem pointsSystem,
        IViewerFeature viewerFeature,
        IServiceBackbone serviceBackbone,
        Application.Notifications.IPenguinDispatcher dispatcher,
        ICommandHandler commandHandler,
        IDefaultCommandTriggerService defaultCommandTriggerService,
        PenguinTwitchBot.Bot.Commands.Games.IGameSettingsService gameSettingsService
            ) : BaseCommandService(serviceBackbone, commandHandler, "Steal", dispatcher), IHostedService
    {
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

            var pointType = await pointsSystem.GetPointTypeForGame(ModuleName);
            var options = await StealGameOptions.LoadAsync(pointType.Id, gameSettingsService);

            var userPasties = await pointsSystem.GetUserPointsByUserIdAndGame(e.UserId, ModuleName);
            if (userPasties.Points < options.MaximumTransfer)
            {
                await ServiceBackbone.ResponseWithMessage(e, string.Format("you don't have enough {0} to steal, you need a minimum of {1}", pointType.Name, options.MaximumTransfer));
                throw new SkipCooldownException();
            }

            await StealFromUser(e, options, pointType.Name);
        }

        private async Task StealFromUser(CommandEventArgs e, StealGameOptions options, string pointTypeName)
        {
            var targetPasties = await pointsSystem.GetUserPointsByUsernameAndGame(e.TargetUser, ModuleName);
            var targetDisplayName = await viewerFeature.GetNameWithTitle(e.TargetUser);
            var amount = (int)Random.Shared.NextInt64(options.MinimumTransfer, (long)options.MaximumTransfer + 1);
            if (targetPasties.Points < options.MaximumTransfer)
            {
                await ServiceBackbone.ResponseWithMessage(e,
                string.Format("{0} is too poor for you to steal from them, instead you give them {1} {2}.", targetDisplayName, amount.ToString("N0"), pointTypeName));
                await MovePoints(e.Name, e.TargetUser, amount);

                // Trigger default command event for too poor
                await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                    "steal",
                    DefaultCommandEventTypes.StealToPoor,
                    e,
                    new Dictionary<string, string>
                    {
                        { "TargetUser", e.TargetUser },
                        { "TargetDisplayName", targetDisplayName },
                        { "Amount", amount.ToString("N0") }
                    });
                return;
            }
            if (StaticTools.NextDouble() * 100 < options.SuccessChancePercent)
            {
                await ServiceBackbone.SendChatMessage(string.Format("{0} successfully stole {1} {2} from {3}",
                e.DisplayName, amount, pointTypeName, targetDisplayName));

                await MovePoints(e.TargetUser, e.Name, amount);

                // Trigger default command event for success
                await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                    "steal",
                    DefaultCommandEventTypes.StealSuccess,
                    e,
                    new Dictionary<string, string>
                    {
                        { "TargetUser", e.TargetUser },
                        { "TargetDisplayName", targetDisplayName },
                        { "Amount", amount.ToString("N0") }
                    });
            }
            else
            {
                await ServiceBackbone.SendChatMessage(string.Format("{0} failed to steal {1} {2} from {3}, {3} gets {1} {2} from {0} instead",
               e.DisplayName, amount, pointTypeName, targetDisplayName));
                await MovePoints(e.Name, e.TargetUser, amount);

                // Trigger default command event for failed
                await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                    "steal",
                    DefaultCommandEventTypes.StealFailed,
                    e,
                    new Dictionary<string, string>
                    {
                        { "TargetUser", e.TargetUser },
                        { "TargetDisplayName", targetDisplayName },
                        { "Amount", amount.ToString("N0") }
                    });
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
