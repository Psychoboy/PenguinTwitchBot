using System.Collections.Concurrent;
using System.Globalization;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games.Curling;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class CurlingStartSessionHandler(ICurlingGameService curlingGameService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.CurlingStartSession;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not CurlingStartSessionType sessionSubAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type CurlingStartSession is not of CurlingStartSessionType class");
            }

            int? durationSeconds = null;
            var resolvedDuration = VariableReplacer.ReplaceVariables(sessionSubAction.DurationSeconds ?? "", variables).Trim();
            if (int.TryParse(resolvedDuration, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDuration) && parsedDuration > 0)
            {
                durationSeconds = parsedDuration;
            }

            var resolvedTheme = VariableReplacer.ReplaceVariables(sessionSubAction.ThemeOverrideId ?? "", variables).Trim();
            var triggeredBy = variables.TryGetValue("User", out var u) && !string.IsNullOrWhiteSpace(u) ? u : "SubAction";

            context?.LogMessage(subActionIndex, $"Starting Curling session for {durationSeconds?.ToString() ?? "default"}s");
            await curlingGameService.StartSessionAsync(durationSeconds, resolvedTheme, triggeredBy);
        }
    }

    public class CurlingStopSessionHandler(ICurlingGameService curlingGameService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.CurlingStopSession;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not CurlingStopSessionType sessionSubAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type CurlingStopSession is not of CurlingStopSessionType class");
            }

            var triggeredBy = variables.TryGetValue("User", out var u) && !string.IsNullOrWhiteSpace(u) ? u : "SubAction";
            context?.LogMessage(subActionIndex, "Stopping Curling session");
            if (sessionSubAction.ClearQueue)
            {
                await curlingGameService.ClearSessionQueueAsync();
            }
            await curlingGameService.StopSessionAsync(triggeredBy);
        }
    }
}

