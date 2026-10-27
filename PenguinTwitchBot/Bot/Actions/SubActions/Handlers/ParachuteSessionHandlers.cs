using System.Collections.Concurrent;
using System.Globalization;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games.Parachute;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ParachuteStartSessionHandler(IParachuteDropService parachuteDropService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ParachuteStartSession;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not ParachuteStartSessionType sessionSubAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type ParachuteStartSession is not of ParachuteStartSessionType class");
            }

            int? durationSeconds = null;
            var resolvedDuration = VariableReplacer.ReplaceVariables(sessionSubAction.DurationSeconds ?? "", variables).Trim();
            if (int.TryParse(resolvedDuration, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDuration) && parsedDuration > 0)
            {
                durationSeconds = parsedDuration;
            }

            var resolvedTheme = VariableReplacer.ReplaceVariables(sessionSubAction.ThemeOverrideId ?? "", variables).Trim();
            var triggeredBy = variables.TryGetValue("User", out var u) && !string.IsNullOrWhiteSpace(u) ? u : "SubAction";

            context?.LogMessage(subActionIndex, $"Starting Parachute session for {durationSeconds?.ToString() ?? "default"}s");
            await parachuteDropService.StartSessionAsync(durationSeconds, resolvedTheme, triggeredBy);
        }
    }

    public class ParachuteStopSessionHandler(IParachuteDropService parachuteDropService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ParachuteStopSession;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not ParachuteStopSessionType sessionSubAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type ParachuteStopSession is not of ParachuteStopSessionType class");
            }

            var triggeredBy = variables.TryGetValue("User", out var u) && !string.IsNullOrWhiteSpace(u) ? u : "SubAction";
            context?.LogMessage(subActionIndex, "Stopping Parachute session");
            if (sessionSubAction.ClearQueue)
            {
                await parachuteDropService.ClearSessionQueueAsync();
            }
            await parachuteDropService.StopSessionAsync(triggeredBy);
        }
    }
}

