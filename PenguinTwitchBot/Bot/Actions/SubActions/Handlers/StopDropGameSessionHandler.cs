using System.Collections.Concurrent;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Balloon;
using PenguinTwitchBot.Bot.Commands.Games.Curling;
using PenguinTwitchBot.Bot.Commands.Games.Golf;
using PenguinTwitchBot.Bot.Commands.Games.Parachute;
using PenguinTwitchBot.Bot.Commands.Games.Plinko;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class StopDropGameSessionHandler(
        IParachuteDropService parachuteDropService,
        IPlinkoDropService plinkoDropService,
        IBalloonAscentService? balloonAscentService = null,
        ICurlingGameService? curlingGameService = null,
        IGolfGameService? golfGameService = null) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.StopDropGameSession;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not StopDropGameSessionType sessionSubAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type StopDropGameSession is not of StopDropGameSessionType class");
            }

            var resolvedGame = VariableReplacer.ReplaceVariables(sessionSubAction.GameType ?? "Parachute", variables).Trim();
            var triggeredBy = variables.TryGetValue("User", out var u) && !string.IsNullOrWhiteSpace(u) ? u : "SubAction";

            if (resolvedGame.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                context?.LogMessage(subActionIndex, "Stopping all drop game sessions");
                await parachuteDropService.ClearSessionQueueAsync();
                await plinkoDropService.ClearSessionQueueAsync();
                await parachuteDropService.StopSessionAsync(triggeredBy);
                await plinkoDropService.StopSessionAsync(triggeredBy);
                if (balloonAscentService != null)
                {
                    await balloonAscentService.ClearSessionQueueAsync();
                    await balloonAscentService.StopSessionAsync(triggeredBy);
                }
                if (curlingGameService != null)
                {
                    await curlingGameService.ClearSessionQueueAsync();
                    await curlingGameService.StopSessionAsync(triggeredBy);
                }
                if (golfGameService != null)
                {
                    await golfGameService.ClearSessionQueueAsync();
                    await golfGameService.StopSessionAsync(triggeredBy);
                }
            }
            else
            {
                var gameType = resolvedGame switch
                {
                    var g when g.Equals("Plinko", StringComparison.OrdinalIgnoreCase) => DropGameType.Plinko,
                    var g when g.Equals("Balloon", StringComparison.OrdinalIgnoreCase) || g.Equals("BalloonAscent", StringComparison.OrdinalIgnoreCase) => DropGameType.Balloon,
                    var g when g.Equals("Curling", StringComparison.OrdinalIgnoreCase) || g.Equals("Shuffleboard", StringComparison.OrdinalIgnoreCase) => DropGameType.Curling,
                    var g when g.Equals("Golf", StringComparison.OrdinalIgnoreCase) || g.Equals("Chip", StringComparison.OrdinalIgnoreCase) || g.Equals("Putt", StringComparison.OrdinalIgnoreCase) => DropGameType.Golf,
                    _ => DropGameType.Parachute
                };

                context?.LogMessage(subActionIndex, $"Stopping {gameType} session");
                if (gameType == DropGameType.Plinko)
                {
                    await plinkoDropService.StopSessionAsync(triggeredBy);
                }
                else if (gameType == DropGameType.Balloon && balloonAscentService != null)
                {
                    await balloonAscentService.StopSessionAsync(triggeredBy);
                }
                else if (gameType == DropGameType.Curling && curlingGameService != null)
                {
                    await curlingGameService.StopSessionAsync(triggeredBy);
                }
                else if (gameType == DropGameType.Golf && golfGameService != null)
                {
                    await golfGameService.StopSessionAsync(triggeredBy);
                }
                else
                {
                    await parachuteDropService.StopSessionAsync(triggeredBy);
                }
            }
        }
    }
}
