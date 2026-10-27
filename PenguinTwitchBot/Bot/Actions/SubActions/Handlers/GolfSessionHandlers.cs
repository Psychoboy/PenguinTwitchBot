using System.Collections.Concurrent;
using System.Globalization;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games.Golf;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class GolfStartSessionHandler(IGolfGameService golfGameService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.GolfStartSession;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not GolfStartSessionType sessionSubAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type GolfStartSession is not of GolfStartSessionType class");
            }

            int? durationSeconds = null;
            var resolvedDuration = VariableReplacer.ReplaceVariables(sessionSubAction.DurationSeconds ?? "", variables).Trim();
            if (int.TryParse(resolvedDuration, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDuration) && parsedDuration > 0)
            {
                durationSeconds = parsedDuration;
            }

            var resolvedTheme = VariableReplacer.ReplaceVariables(sessionSubAction.ThemeOverrideId ?? "", variables).Trim();
            var triggeredBy = variables.TryGetValue("User", out var u) && !string.IsNullOrWhiteSpace(u) ? u : "SubAction";

            context?.LogMessage(subActionIndex, $"Starting Golf session for {durationSeconds?.ToString() ?? "default"}s");
            await golfGameService.StartSessionAsync(durationSeconds, resolvedTheme, triggeredBy);
        }
    }

    public class GolfStopSessionHandler(IGolfGameService golfGameService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.GolfStopSession;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not GolfStopSessionType sessionSubAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type GolfStopSession is not of GolfStopSessionType class");
            }

            var triggeredBy = variables.TryGetValue("User", out var u) && !string.IsNullOrWhiteSpace(u) ? u : "SubAction";
            context?.LogMessage(subActionIndex, "Stopping Golf session");
            if (sessionSubAction.ClearQueue)
            {
                await golfGameService.ClearSessionQueueAsync();
            }
            await golfGameService.StopSessionAsync(triggeredBy);
        }
    }
}

