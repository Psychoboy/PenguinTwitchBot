using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Commands.Misc;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class TimerGroupSetEnabledStateHandler(ILogger<TimerGroupSetEnabledStateHandler> logger, AutoTimers timerService) : ISubActionHandler
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

            timerGroupSetEnabled.TimerGroupName = timerGroup.Name;
            timerGroup.Active = timerGroupSetEnabled.IsEnabled;
            await timerService.UpdateTimerGroup(timerGroup);

            logger.LogInformation("Timer group {TimerGroupName} (ID: {TimerGroupId}) set to {State}",
                timerGroup.Name, timerGroup.Id, timerGroupSetEnabled.IsEnabled ? "enabled" : "disabled");
        }
    }
}
