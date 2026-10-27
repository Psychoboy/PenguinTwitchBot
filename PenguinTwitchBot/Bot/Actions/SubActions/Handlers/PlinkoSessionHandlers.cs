using System.Collections.Concurrent;
using System.Globalization;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games.Plinko;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class PlinkoStartSessionHandler(IPlinkoDropService plinkoDropService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.PlinkoStartSession;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not PlinkoStartSessionType sessionSubAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type PlinkoStartSession is not of PlinkoStartSessionType class");
            }

            int? durationSeconds = null;
            var resolvedDuration = VariableReplacer.ReplaceVariables(sessionSubAction.DurationSeconds ?? "", variables).Trim();
            if (int.TryParse(resolvedDuration, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDuration) && parsedDuration > 0)
            {
                durationSeconds = parsedDuration;
            }

            var resolvedTheme = VariableReplacer.ReplaceVariables(sessionSubAction.ThemeOverrideId ?? "", variables).Trim();
            var triggeredBy = variables.TryGetValue("User", out var u) && !string.IsNullOrWhiteSpace(u) ? u : "SubAction";

            context?.LogMessage(subActionIndex, $"Starting Plinko session for {durationSeconds?.ToString() ?? "default"}s");
            await plinkoDropService.StartSessionAsync(durationSeconds, resolvedTheme, triggeredBy);
        }
    }

    public class PlinkoStopSessionHandler(IPlinkoDropService plinkoDropService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.PlinkoStopSession;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not PlinkoStopSessionType sessionSubAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type PlinkoStopSession is not of PlinkoStopSessionType class");
            }

            var triggeredBy = variables.TryGetValue("User", out var u) && !string.IsNullOrWhiteSpace(u) ? u : "SubAction";
            context?.LogMessage(subActionIndex, "Stopping Plinko session");
            if (sessionSubAction.ClearQueue)
            {
                await plinkoDropService.ClearSessionQueueAsync();
            }
            await plinkoDropService.StopSessionAsync(triggeredBy);
        }
    }
}

