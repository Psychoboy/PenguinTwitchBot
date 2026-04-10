using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;
using DotNetTwitchBot.Bot.TwitchServices;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class UptimeHandler(ITwitchService twitchService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.Uptime;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not UptimeType)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for UptimeHandler: {SubActionType}", subAction.GetType().Name);
            }

            var streamTime = await twitchService.StreamStartedAt();
            if (streamTime == DateTime.MinValue)
            {
                variables["Uptime"] = "Offline";

            }
            else
            {
                var currentTime = DateTime.UtcNow;
                var totalTime = currentTime - streamTime;
                variables["Uptime"] = totalTime.ToString(@"hh\:mm\:ss");
            }
        }
    }
}

