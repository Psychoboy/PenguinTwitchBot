using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Balloon;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.Commands.Games.Curling;
using PenguinTwitchBot.Bot.Commands.Games.Golf;
using PenguinTwitchBot.Bot.Commands.Games.Parachute;
using PenguinTwitchBot.Bot.Commands.Games.Plinko;
using PenguinTwitchBot.Bot.Commands.Games.Racing;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Commands;

namespace PenguinTwitchBot.Bot.Commands
{
    public class BaseCommandRunHandler(
        ICommandHandler commandHandler, 
        IServiceScopeFactory serviceScopeFactory, 
        IDropGamesSettingsService dropGamesSettingsService,
        IParachuteDropService parachuteDropService,
        IPlinkoDropService plinkoDropService,
        IBalloonAscentService balloonAscentService,
        ICurlingGameService curlingGameService,
        IGolfGameService golfGameService,
        IStarshipExpeditionService starshipExpeditionService,
        IStarshipExpeditionSettingsStore starshipSettingsStore,
        ILogger<BaseCommandRunHandler> logger,
        IRacingGameService? racingGameService = null) : Application.Notifications.INotificationHandler<RunCommandNotification>
    {
        public async Task Handle(RunCommandNotification notification, CancellationToken cancellationToken)
        {
            var eventArgs = notification.EventArgs;
            if(eventArgs == null)
            {
                logger.LogError("RunCommandNotification EventArgs is null");
                return;
            }

            var commandService = commandHandler.GetCommand(eventArgs.Command);
            if (commandService?.CommandProperties is DefaultCommand { } defaultCommand &&
                !await IsGameAvailableForCommandAsync(defaultCommand, eventArgs))
            {
                logger.LogInformation("Ignored chat command {CommandName} because its minigame is disabled", defaultCommand.CommandName);
                return;
            }

            if (commandService != null && commandService.CommandProperties.Disabled == false && CommandHandler.CheckIfAllowedInSharedChat(eventArgs, commandService.CommandProperties))
            {
                if (commandService.CommandProperties is DefaultCommand dc &&
                    dc.ModuleName is "ParachuteDrop" or "PlinkoDrop" or "BalloonAscent" or "Curling" or "Golf" or "Racing")
                {
                    eventArgs.SkipLock = true;
                }

                var action = new ActionType
                {
                    Name = commandService.CommandProperties.CommandName,
                    Group = "Default Command",
                    Enabled = true,
                    RandomAction = false,
                    ConcurrentAction = false,
                    OnlineOnly = false,
                    QueueName = QueueManager.DefaultQueueName
                };

                var subAction = new RuntimeDefaultCommandType();
                action.SubActions = new List<SubActionType> { subAction };
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var actionService = scope.ServiceProvider.GetRequiredService<IAction>();
                await actionService.EnqueueAction(CommandEventArgsConverter.ToDictionary(eventArgs), action);
            }
        }

        private async Task<bool> IsGameAvailableForCommandAsync(DefaultCommand command, CommandEventArgs eventArgs)
        {
            var commandArgument = eventArgs.Args.FirstOrDefault() ?? eventArgs.Arg.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            var dropSettings = await dropGamesSettingsService.GetSettingsAsync();
            var isStartCommand = commandArgument?.Equals("start", StringComparison.OrdinalIgnoreCase) == true;

            return command.ModuleName switch
            {
                "ParachuteDrop" => IsDropGameAvailable(dropSettings.Parachute.GameEnabled, parachuteDropService.GetCurrentSession()?.IsActive, IsStopCommand(dropSettings.Parachute.ControlCommandMinimumRank, commandArgument, eventArgs), isStartCommand),
                "PlinkoDrop" => IsDropGameAvailable(dropSettings.Plinko.GameEnabled, plinkoDropService.GetCurrentSession()?.IsActive, IsStopCommand(dropSettings.Plinko.ControlCommandMinimumRank, commandArgument, eventArgs), isStartCommand),
                "BalloonAscent" => IsDropGameAvailable(dropSettings.Balloon.GameEnabled, balloonAscentService.GetCurrentSession()?.IsActive, IsStopCommand(dropSettings.Balloon.ControlCommandMinimumRank, commandArgument, eventArgs), isStartCommand),
                "Curling" => IsDropGameAvailable(dropSettings.Curling.GameEnabled, curlingGameService.GetCurrentSession()?.IsActive, IsStopCommand(dropSettings.Curling.ControlCommandMinimumRank, commandArgument, eventArgs), isStartCommand),
                "Golf" => IsDropGameAvailable(dropSettings.Golf.GameEnabled, golfGameService.GetCurrentSession()?.IsActive, IsStopCommand(dropSettings.Golf.ControlCommandMinimumRank, commandArgument, eventArgs), isStartCommand),
                "Racing" => IsDropGameAvailable(dropSettings.Racing.GameEnabled, racingGameService?.GetCurrentSession()?.IsActive, IsRacingStopCommand(dropSettings.Racing.ControlCommandMinimumRank, commandArgument, eventArgs), isStartCommand),
                "StarshipExpedition" => await IsStarshipAvailableForCommandAsync(commandArgument, isStartCommand, eventArgs),
                _ => true
            };
        }

        private static bool IsStopCommand(Rank minRank, string? commandArgument, CommandEventArgs eventArgs) =>
            eventArgs.MeetsRank(minRank) && commandArgument?.Equals("stop", StringComparison.OrdinalIgnoreCase) == true;

        private static bool IsRacingStopCommand(Rank minRank, string? commandArgument, CommandEventArgs eventArgs) =>
            eventArgs.MeetsRank(minRank) && (commandArgument?.Equals("stop", StringComparison.OrdinalIgnoreCase) == true || commandArgument?.Equals("cancel", StringComparison.OrdinalIgnoreCase) == true);

        private static bool IsDropGameAvailable(
            bool isEnabled,
            bool? hasActiveSession,
            bool isStopOrStatusCommand,
            bool isStartCommand)
        {
            if (isStopOrStatusCommand) return true;
            if (!isStartCommand && hasActiveSession == true) return true;
            return isEnabled;
        }

        private async Task<bool> IsStarshipAvailableForCommandAsync(string? commandArgument, bool isStartCommand, CommandEventArgs eventArgs)
        {
            if (commandArgument?.Equals("status", StringComparison.OrdinalIgnoreCase) == true)
                return true;

            var starshipSettings = await starshipSettingsStore.LoadAsync();
            var isControlCommand = eventArgs.MeetsRank(starshipSettings.ControlCommandMinimumRank) &&
                (commandArgument?.Equals("stop", StringComparison.OrdinalIgnoreCase) == true ||
                 commandArgument?.Equals("advance", StringComparison.OrdinalIgnoreCase) == true);
            if (isControlCommand)
                return true;

            var snapshot = await starshipExpeditionService.GetSnapshotAsync();
            if (!isStartCommand && snapshot?.Status is StarshipExpeditionStatus.Boarding or StarshipExpeditionStatus.Cruising or StarshipExpeditionStatus.EventOpen)
                return true;

            return starshipSettings.GameEnabled;
        }
    }
}
