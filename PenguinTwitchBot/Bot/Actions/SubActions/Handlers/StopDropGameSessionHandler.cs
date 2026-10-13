using System.Collections.Concurrent;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class StopDropGameSessionHandler(IDropGamesService dropGamesService) : ISubActionHandler
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
                context?.LogMessage(subActionIndex, "Stopping Parachute and Plinko sessions");
                await dropGamesService.StopSessionAsync(DropGameType.Parachute, triggeredBy);
                await dropGamesService.StopSessionAsync(DropGameType.Plinko, triggeredBy);
            }
            else
            {
                var gameType = resolvedGame.Equals("Plinko", StringComparison.OrdinalIgnoreCase)
                    ? DropGameType.Plinko
                    : DropGameType.Parachute;

                context?.LogMessage(subActionIndex, $"Stopping {gameType} session");
                await dropGamesService.StopSessionAsync(gameType, triggeredBy);
            }
        }
    }
}

