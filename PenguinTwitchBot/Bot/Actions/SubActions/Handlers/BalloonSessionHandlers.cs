using System.Collections.Concurrent;
using System.Globalization;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games.Balloon;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class BalloonStartSessionHandler(IBalloonAscentService balloonAscentService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.BalloonStartSession;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not BalloonStartSessionType sessionSubAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type BalloonStartSession is not of BalloonStartSessionType class");
            }

            int? durationSeconds = null;
            var resolvedDuration = VariableReplacer.ReplaceVariables(sessionSubAction.DurationSeconds ?? "", variables).Trim();
            if (int.TryParse(resolvedDuration, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDuration) && parsedDuration > 0)
            {
                durationSeconds = parsedDuration;
            }

            var resolvedTheme = VariableReplacer.ReplaceVariables(sessionSubAction.ThemeOverrideId ?? "", variables).Trim();
            var triggeredBy = variables.TryGetValue("User", out var u) && !string.IsNullOrWhiteSpace(u) ? u : "SubAction";

            context?.LogMessage(subActionIndex, $"Starting Balloon session for {durationSeconds?.ToString() ?? "default"}s");
            await balloonAscentService.StartSessionAsync(durationSeconds, resolvedTheme, triggeredBy);
        }
    }

    public class BalloonStopSessionHandler(IBalloonAscentService balloonAscentService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.BalloonStopSession;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not BalloonStopSessionType sessionSubAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type BalloonStopSession is not of BalloonStopSessionType class");
            }

            var triggeredBy = variables.TryGetValue("User", out var u) && !string.IsNullOrWhiteSpace(u) ? u : "SubAction";
            context?.LogMessage(subActionIndex, "Stopping Balloon session");
            if (sessionSubAction.ClearQueue)
            {
                await balloonAscentService.ClearSessionQueueAsync();
            }
            await balloonAscentService.StopSessionAsync(triggeredBy);
        }
    }
}

