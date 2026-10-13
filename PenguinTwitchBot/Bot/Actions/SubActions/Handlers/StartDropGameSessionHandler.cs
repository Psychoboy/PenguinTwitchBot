using System.Collections.Concurrent;
using System.Globalization;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class StartDropGameSessionHandler(IDropGamesService dropGamesService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.StartDropGameSession;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not StartDropGameSessionType sessionSubAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type StartDropGameSession is not of StartDropGameSessionType class");
            }

            var resolvedGame = VariableReplacer.ReplaceVariables(sessionSubAction.GameType ?? "Parachute", variables).Trim();
            var gameType = resolvedGame.Equals("Plinko", StringComparison.OrdinalIgnoreCase)
                ? DropGameType.Plinko
                : DropGameType.Parachute;

            int? durationSeconds = null;
            var resolvedDuration = VariableReplacer.ReplaceVariables(sessionSubAction.DurationSeconds ?? "", variables).Trim();
            if (int.TryParse(resolvedDuration, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDuration) && parsedDuration > 0)
            {
                durationSeconds = parsedDuration;
            }

            var resolvedTheme = VariableReplacer.ReplaceVariables(sessionSubAction.ThemeOverrideId ?? "", variables).Trim();
            var triggeredBy = variables.TryGetValue("User", out var u) && !string.IsNullOrWhiteSpace(u) ? u : "SubAction";

            context?.LogMessage(subActionIndex, $"Starting {gameType} session for {durationSeconds?.ToString() ?? "default"}s");
            await dropGamesService.StartSessionAsync(gameType, durationSeconds, resolvedTheme, triggeredBy);
        }
    }
}

