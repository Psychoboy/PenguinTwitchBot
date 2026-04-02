using DotNetTwitchBot.Bot.Models.Actions.SubActions;
using DotNetTwitchBot.Bot.TwitchServices;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class UptimeHandler(ILogger<UptimeHandler> logger, ITwitchService twitchService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.Uptime;

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not UptimeType)
            {
                logger.LogError("Invalid sub action type for UptimeHandler: {SubActionType}", subAction.GetType().Name);
                return;
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
