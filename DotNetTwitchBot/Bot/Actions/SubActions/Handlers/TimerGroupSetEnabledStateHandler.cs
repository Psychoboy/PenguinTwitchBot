using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Commands.Misc;
using DotNetTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class TimerGroupSetEnabledStateHandler(
        ILogger<TimerGroupSetEnabledStateHandler> logger, 
        AutoTimers timerService,
        ISubActionExecutionContextAccessor contextAccessor) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.TimerGroupSetEnabledState;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables)
        {
            if (subAction is not TimerGroupSetEnabledStateType timerGroupSetEnabled)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type provided to TimerGroupSetEnabledStateHandler");
            }

            if (!timerGroupSetEnabled.TimerGroupId.HasValue)
            {
                throw new SubActionHandlerException(subAction, "No timer group id provided for TimerGroupSetEnabledStateHandler");
            }

            var timerGroup = await timerService.GetTimerGroupAsync(timerGroupSetEnabled.TimerGroupId.Value);
            if (timerGroup == null)
            {
                throw new SubActionHandlerException(subAction, "No timer group found with id {TimerGroupId} for TimerGroupSetEnabledStateHandler", timerGroupSetEnabled.TimerGroupId.Value);
            }
            if (timerGroupSetEnabled.IsEnabled)
            {
                timerGroup = await timerService.UpdateNextRun(timerGroup);
            }
            timerGroupSetEnabled.TimerGroupName = timerGroup.Name;
            timerGroup.Active = timerGroupSetEnabled.IsEnabled;
            await timerService.UpdateTimerGroup(timerGroup);
            var context = contextAccessor.ExecutionContext;
            var state = timerGroupSetEnabled.IsEnabled ? "enabled" : "disabled";
            context?.LogMessage(contextAccessor.CurrentSubActionIndex, $"Timer group {timerGroup.Name} (ID: {timerGroup.Id}) set to {state}");
        }
    }
}
