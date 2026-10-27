using System.Collections.Concurrent;
using System.Globalization;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Balloon;
using PenguinTwitchBot.Bot.Commands.Games.Curling;
using PenguinTwitchBot.Bot.Commands.Games.Golf;
using PenguinTwitchBot.Bot.Commands.Games.Parachute;
using PenguinTwitchBot.Bot.Commands.Games.Plinko;
using PenguinTwitchBot.Bot.Commands.Games.Racing;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class StartDropGameSessionHandler(
        IParachuteDropService parachuteDropService,
        IPlinkoDropService plinkoDropService,
        IBalloonAscentService? balloonAscentService = null,
        ICurlingGameService? curlingGameService = null,
        IGolfGameService? golfGameService = null,
        IRacingGameService? racingGameService = null) : ISubActionHandler
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
            RacingGameMode? racingModeOverride = null;
            RacingTrackType? racingTrackOverride = null;

            if (resolvedGame.Equals("RacingDerby", StringComparison.OrdinalIgnoreCase) ||
                resolvedGame.Equals("DestructionDerby", StringComparison.OrdinalIgnoreCase) ||
                resolvedGame.Equals("Derby", StringComparison.OrdinalIgnoreCase))
            {
                racingModeOverride = RacingGameMode.DestructionDerby;
                racingTrackOverride = RacingTrackType.DerbyBowl;
            }
            else if (resolvedGame.Equals("RacingElimination", StringComparison.OrdinalIgnoreCase) ||
                     resolvedGame.Equals("Elimination", StringComparison.OrdinalIgnoreCase))
            {
                racingModeOverride = RacingGameMode.Elimination;
            }
            else if (resolvedGame.Equals("RacingGrandPrix", StringComparison.OrdinalIgnoreCase) ||
                     resolvedGame.Equals("GrandPrix", StringComparison.OrdinalIgnoreCase))
            {
                racingModeOverride = RacingGameMode.GrandPrix;
            }

            var gameType = resolvedGame switch
            {
                var g when g.Equals("Plinko", StringComparison.OrdinalIgnoreCase) => DropGameType.Plinko,
                var g when g.Equals("Balloon", StringComparison.OrdinalIgnoreCase) || g.Equals("BalloonAscent", StringComparison.OrdinalIgnoreCase) => DropGameType.Balloon,
                var g when g.Equals("Curling", StringComparison.OrdinalIgnoreCase) || g.Equals("Shuffleboard", StringComparison.OrdinalIgnoreCase) => DropGameType.Curling,
                var g when g.Equals("Golf", StringComparison.OrdinalIgnoreCase) || g.Equals("Chip", StringComparison.OrdinalIgnoreCase) || g.Equals("Putt", StringComparison.OrdinalIgnoreCase) => DropGameType.Golf,
                var g when g.Equals("Racing", StringComparison.OrdinalIgnoreCase) || g.Equals("Race", StringComparison.OrdinalIgnoreCase) || g.Equals("Kart", StringComparison.OrdinalIgnoreCase) || racingModeOverride.HasValue => DropGameType.Racing,
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
            else if (gameType == DropGameType.Golf && golfGameService != null)
            {
                await golfGameService.StartSessionAsync(durationSeconds, resolvedTheme, triggeredBy);
            }
            else if (gameType == DropGameType.Racing && racingGameService != null)
            {
                if (racingModeOverride.HasValue || racingTrackOverride.HasValue)
                {
                    await racingGameService.StartRaceLobbyAsync(durationSeconds, resolvedTheme, triggeredBy, racingModeOverride, racingTrackOverride);
                }
                else
                {
                    await racingGameService.StartRaceLobbyAsync(durationSeconds, resolvedTheme, triggeredBy);
                }
            }
            else
            {
                await parachuteDropService.StartSessionAsync(durationSeconds, resolvedTheme, triggeredBy);
            }
        }
    }
}
