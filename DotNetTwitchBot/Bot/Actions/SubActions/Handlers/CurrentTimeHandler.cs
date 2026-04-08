using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class CurrentTimeHandler() : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.CurrentTime;

        public Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables)
        {
            if (subAction is not CurrentTimeType)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type CurrentTime is not of CurrentTimeType class");
            }
            var time = DateTime.Now.ToString("h:mm:ss tt");
            variables["current_time"] = time;
            return Task.CompletedTask;
        }
    }
}
