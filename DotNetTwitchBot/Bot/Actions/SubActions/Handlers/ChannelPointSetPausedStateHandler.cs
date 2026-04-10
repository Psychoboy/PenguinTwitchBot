using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;
using DotNetTwitchBot.Bot.TwitchServices;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ChannelPointSetPausedStateHandler(
        ILogger<ChannelPointSetPausedStateHandler> logger,
        ITwitchService twitchService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ChannelPointSetPausedState;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if(subAction is not ChannelPointSetPausedStateType setPausedStateType)
            {
                throw new SubActionHandlerException(subAction, $"Invalid sub action type: {subAction.GetType().Name}");
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

