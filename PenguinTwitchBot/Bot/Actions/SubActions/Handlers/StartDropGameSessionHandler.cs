using System.Collections.Concurrent;
using System.Globalization;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Balloon;
using PenguinTwitchBot.Bot.Commands.Games.Curling;
using PenguinTwitchBot.Bot.Commands.Games.Parachute;
using PenguinTwitchBot.Bot.Commands.Games.Plinko;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class StartDropGameSessionHandler(
        IParachuteDropService parachuteDropService,
        IPlinkoDropService plinkoDropService,
        IBalloonAscentService? balloonAscentService = null,
        ICurlingGameService? curlingGameService = null) : ISubActionHandler
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
            var gameType = resolvedGame switch
            {
                var g when g.Equals("Plinko", StringComparison.OrdinalIgnoreCase) => DropGameType.Plinko,
                var g when g.Equals("Balloon", StringComparison.OrdinalIgnoreCase) || g.Equals("BalloonAscent", StringComparison.OrdinalIgnoreCase) => DropGameType.Balloon,
                var g when g.Equals("Curling", StringComparison.OrdinalIgnoreCase) || g.Equals("Shuffleboard", StringComparison.OrdinalIgnoreCase) => DropGameType.Curling,
                _ => DropGameType.Parachute
            };

            int? durationSeconds = null;
            var resolvedDuration = VariableReplacer.ReplaceVariables(sessionSubAction.DurationSeconds ?? "", variables).Trim();
            if (int.TryParse(resolvedDuration, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDuration) && parsedDuration > 0)
            {
                durationSeconds = parsedDuration;
            }

            var resolvedTheme = VariableReplacer.ReplaceVariables(sessionSubAction.ThemeOverrideId ?? "", variables).Trim();
            var triggeredBy = variables.TryGetValue("User", out var u) && !string.IsNullOrWhiteSpace(u) ? u : "SubAction";

            context?.LogMessage(subActionIndex, $"Starting {gameType} session for {durationSeconds?.ToString() ?? "default"}s");
            if (gameType == DropGameType.Plinko)
            {
                await plinkoDropService.StartSessionAsync(durationSeconds, resolvedTheme, triggeredBy);
            }
            else if (gameType == DropGameType.Balloon && balloonAscentService != null)
            {
                await balloonAscentService.StartSessionAsync(durationSeconds, resolvedTheme, triggeredBy);
            }
            else if (gameType == DropGameType.Curling && curlingGameService != null)
            {
                await curlingGameService.StartSessionAsync(durationSeconds, resolvedTheme, triggeredBy);
            }
            else
            {
                await parachuteDropService.StartSessionAsync(durationSeconds, resolvedTheme, triggeredBy);
            }
        }
    }
}
