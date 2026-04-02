using DotNetTwitchBot.Bot.Actions.SubActions.Types;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class CurrentTimeHandler(ILogger<CurrentTimeHandler> logger) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.CurrentTime;

        public Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not CurrentTimeType)
            {
                logger.LogWarning("SubAction with type CurrentTime is not of CurrentTimeType class");
                return Task.CompletedTask;
            }
            var time = DateTime.Now.ToString("h:mm:ss tt");
            variables["current_time"] = time;
            return Task.CompletedTask;
        }
    }
}
