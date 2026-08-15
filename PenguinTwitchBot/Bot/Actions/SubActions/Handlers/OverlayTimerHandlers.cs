using PenguinTwitchBot.Bot.Overlay;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using System.Collections.Concurrent;
using System.Globalization;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    /// <summary>
    /// Parses the "seconds or hh:mm:ss" duration format shared by the overlay timer sub-actions.
    /// </summary>
    internal static class OverlayTimerDuration
    {
        public static bool TryParse(string? value, out double seconds)
        {
            seconds = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;

            value = value.Trim();

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
                return true;

            if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var timeSpan))
            {
                seconds = timeSpan.TotalSeconds;
                return true;
            }

            return false;
        }
    }

    public class OverlayTimerStartHandler(IStreamTimerService timerService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.OverlayTimerStart;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not OverlayTimerStartType start)
                throw new SubActionHandlerException(subAction, $"Expected {nameof(OverlayTimerStartType)} but got {subAction.GetType().Name}");

            double? startSeconds = null;
            var rawStartTime = VariableReplacer.ReplaceVariables(start.StartTime, variables);
            if (!string.IsNullOrWhiteSpace(rawStartTime))
            {
                if (OverlayTimerDuration.TryParse(rawStartTime, out var parsed))
                {
                    startSeconds = parsed;
                }
                else
                {
                    context?.LogMessage(subActionIndex, $"Invalid start time value: {rawStartTime}");
                }
            }

            await timerService.StartAsync(start.Direction, startSeconds, start.ResetOnStart);
            context?.LogMessage(subActionIndex, $"Started overlay timer counting {start.Direction}");
        }
    }

    public class OverlayTimerStopHandler(IStreamTimerService timerService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.OverlayTimerStop;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not OverlayTimerStopType stop)
                throw new SubActionHandlerException(subAction, $"Expected {nameof(OverlayTimerStopType)} but got {subAction.GetType().Name}");

            await timerService.StopAsync(stop.ResetOnStop);
            context?.LogMessage(subActionIndex, stop.ResetOnStop ? "Stopped and reset overlay timer" : "Stopped overlay timer");
        }
    }

    public class OverlayTimerAddTimeHandler(IStreamTimerService timerService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.OverlayTimerAddTime;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not OverlayTimerAddTimeType addTime)
                throw new SubActionHandlerException(subAction, $"Expected {nameof(OverlayTimerAddTimeType)} but got {subAction.GetType().Name}");

            var raw = VariableReplacer.ReplaceVariables(addTime.Amount, variables);
            if (!OverlayTimerDuration.TryParse(raw, out var seconds))
            {
                context?.LogMessage(subActionIndex, $"Invalid time value: {raw}");
                return;
            }

            await timerService.AddTimeAsync(seconds);
            context?.LogMessage(subActionIndex, $"Added {seconds} seconds to the overlay timer");
        }
    }

    public class OverlayTimerRemoveTimeHandler(IStreamTimerService timerService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.OverlayTimerRemoveTime;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not OverlayTimerRemoveTimeType removeTime)
                throw new SubActionHandlerException(subAction, $"Expected {nameof(OverlayTimerRemoveTimeType)} but got {subAction.GetType().Name}");

            var raw = VariableReplacer.ReplaceVariables(removeTime.Amount, variables);
            if (!OverlayTimerDuration.TryParse(raw, out var seconds))
            {
                context?.LogMessage(subActionIndex, $"Invalid time value: {raw}");
                return;
            }

            await timerService.RemoveTimeAsync(seconds);
            context?.LogMessage(subActionIndex, $"Removed {seconds} seconds from the overlay timer");
        }
    }
}
