using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Commands.Misc;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class TimerGroupSetEnabledStateHandler(ILogger<TimerGroupSetEnabledStateHandler> logger, AutoTimers timerService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.TimerGroupSetEnabledState;

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not TimerGroupSetEnabledStateType timerGroupSetEnabled)
            {
                logger.LogError("Invalid sub action type provided to TimerGroupSetEnabledStateHandler");
                return;
            }

            if (!timerGroupSetEnabled.TimerGroupId.HasValue)
            {
                logger.LogError("No timer group id provided for TimerGroupSetEnabledStateHandler");
                return;
            }

            var timerGroup = await timerService.GetTimerGroupAsync(timerGroupSetEnabled.TimerGroupId.Value);
            if (timerGroup == null)
            {
                logger.LogError("No timer group found with id {TimerGroupId} for TimerGroupSetEnabledStateHandler", timerGroupSetEnabled.TimerGroupId.Value);
                return;
            }

            timerGroupSetEnabled.TimerGroupName = timerGroup.Name;
            timerGroup.Active = timerGroupSetEnabled.IsEnabled;
            await timerService.UpdateTimerGroup(timerGroup);

            logger.LogInformation("Timer group {TimerGroupName} (ID: {TimerGroupId}) set to {State}",
                timerGroup.Name, timerGroup.Id, timerGroupSetEnabled.IsEnabled ? "enabled" : "disabled");
        }
    }
}
