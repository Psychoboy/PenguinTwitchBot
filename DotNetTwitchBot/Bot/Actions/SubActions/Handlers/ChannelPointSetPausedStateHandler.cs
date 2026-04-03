using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.TwitchServices;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ChannelPointSetPausedStateHandler(
        ILogger<ChannelPointSetPausedStateHandler> logger,
        ITwitchService twitchService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => throw new NotImplementedException();

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if(subAction is not ChannelPointSetPausedStateType setPausedStateType)
            {
                logger.LogError("Invalid sub action type: {SubActionType}", subAction.GetType().Name);
                return;
            }

            logger.LogInformation("Setting channel points paused state to {IsPaused}", setPausedStateType.IsPaused);

            var channelPoints = await twitchService.GetChannelPointRewards(true);
            foreach (var channelPoint in channelPoints)
            {
                if(channelPoint.Title.Equals(setPausedStateType.Text, StringComparison.OrdinalIgnoreCase))
                {
                    if(channelPoint.IsPaused != setPausedStateType.IsPaused)
                    {
                        UpdateCustomRewardRequest updateRequest = new UpdateCustomRewardRequest
                        {
                            IsPaused = setPausedStateType.IsPaused
                        };
                        await twitchService.UpdateChannelPointReward(channelPoint.Id, updateRequest);
                        logger.LogInformation("Channel points '{Title}' paused state set to {IsPaused}", channelPoint.Title, setPausedStateType.IsPaused);
                    }
                    else
                    {
                        logger.LogInformation("Channel points '{Title}' already has paused state {IsPaused}", channelPoint.Title, setPausedStateType.IsPaused);
                    }
                    break;
                }
            }
        }
    }
}
