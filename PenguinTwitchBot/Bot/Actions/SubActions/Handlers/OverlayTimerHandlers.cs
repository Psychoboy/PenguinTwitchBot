using PenguinTwitchBot.Bot.Overlay;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class OverlayTimerStartHandler(IStreamTimerService timerService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.OverlayTimerStart;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not OverlayTimerStartType start)
                throw new SubActionHandlerException(subAction, $"Expected {nameof(OverlayTimerStartType)} but got {subAction.GetType().Name}");

            var timerName = VariableReplacer.ReplaceVariables(start.TimerName, variables);
            if (string.IsNullOrWhiteSpace(timerName)) timerName = "Default";

            double? startSeconds = null;
            var rawStartTime = VariableReplacer.ReplaceVariables(start.StartTime, variables);
            if (!string.IsNullOrWhiteSpace(rawStartTime))
            {
                if (TimerDuration.TryParse(rawStartTime, out var parsed))
                {
                    startSeconds = parsed;
                }
                else
                {
                    context?.LogMessage(subActionIndex, $"Invalid start time value: {rawStartTime}");
                }
            }

            await timerService.StartTimerAsync(timerName, start.Direction, startSeconds, start.ResetOnStart, start.OnZeroActionId);
            context?.LogMessage(subActionIndex, $"Started overlay timer '{timerName}' counting {start.Direction}");
        }
    }

    public class OverlayTimerStopHandler(IStreamTimerService timerService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.OverlayTimerStop;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not OverlayTimerStopType stop)
                throw new SubActionHandlerException(subAction, $"Expected {nameof(OverlayTimerStopType)} but got {subAction.GetType().Name}");

            var timerName = VariableReplacer.ReplaceVariables(stop.TimerName, variables);
            if (string.IsNullOrWhiteSpace(timerName)) timerName = "Default";

            await timerService.StopTimerAsync(timerName, stop.ResetOnStop);
            context?.LogMessage(subActionIndex, stop.ResetOnStop ? $"Stopped and reset overlay timer '{timerName}'" : $"Stopped overlay timer '{timerName}'");
        }
    }

    public class OverlayTimerVisibilityHandler(IStreamTimerService timerService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.OverlayTimerVisibility;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not OverlayTimerVisibilityType visibility)
                throw new SubActionHandlerException(subAction, $"Expected {nameof(OverlayTimerVisibilityType)} but got {subAction.GetType().Name}");

            var timerName = VariableReplacer.ReplaceVariables(visibility.TimerName, variables);
            if (string.IsNullOrWhiteSpace(timerName)) timerName = "Default";

            var hidden = visibility.Visibility switch
            {
                OverlayTimerVisibilityType.Show => false,
                OverlayTimerVisibilityType.Toggle => !timerService.GetState(timerName).IsHidden,
                _ => true
            };

            await timerService.SetHiddenAsync(timerName, hidden);
            context?.LogMessage(subActionIndex, $"{(hidden ? "Hid" : "Showed")} overlay timer '{timerName}'");
        }
    }

    public class OverlayTimerAddTimeHandler(IStreamTimerService timerService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.OverlayTimerAddTime;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not OverlayTimerAddTimeType addTime)
                throw new SubActionHandlerException(subAction, $"Expected {nameof(OverlayTimerAddTimeType)} but got {subAction.GetType().Name}");

            var timerName = VariableReplacer.ReplaceVariables(addTime.TimerName, variables);
            if (string.IsNullOrWhiteSpace(timerName)) timerName = "Default";

            var raw = VariableReplacer.ReplaceVariables(addTime.Amount, variables);
            if (!TimerDuration.TryParse(raw, out var seconds))
            {
                context?.LogMessage(subActionIndex, $"Invalid time value: {raw}");
                return;
            }

            await timerService.AddTimeAsync(timerName, seconds);
            context?.LogMessage(subActionIndex, $"Added {seconds} seconds to overlay timer '{timerName}'");
        }
    }

    public class OverlayTimerRemoveTimeHandler(IStreamTimerService timerService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.OverlayTimerRemoveTime;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not OverlayTimerRemoveTimeType removeTime)
                throw new SubActionHandlerException(subAction, $"Expected {nameof(OverlayTimerRemoveTimeType)} but got {subAction.GetType().Name}");

            var timerName = VariableReplacer.ReplaceVariables(removeTime.TimerName, variables);
            if (string.IsNullOrWhiteSpace(timerName)) timerName = "Default";

            var raw = VariableReplacer.ReplaceVariables(removeTime.Amount, variables);
            if (!TimerDuration.TryParse(raw, out var seconds))
            {
                context?.LogMessage(subActionIndex, $"Invalid time value: {raw}");
                return;
            }

            await timerService.RemoveTimeAsync(timerName, seconds);
            context?.LogMessage(subActionIndex, $"Removed {seconds} seconds from overlay timer '{timerName}'");
        }
    }
}
