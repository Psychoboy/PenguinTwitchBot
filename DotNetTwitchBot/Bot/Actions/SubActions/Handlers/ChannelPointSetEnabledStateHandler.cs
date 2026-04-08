using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.TwitchServices;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ChannelPointSetEnabledStateHandler(
        ILogger<ChannelPointSetEnabledStateHandler> logger,
        ITwitchService twitchService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ChannelPointSetEnabledState;

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if(subAction is not ChannelPointSetEnabledStateType channelPointSetEnabled)
            {
                throw new SubActionHandlerException(subAction, $"Unexpected sub action type: {subAction.GetType().Name}");
            }

            logger.LogInformation("Trying to {state} {title} channel point reward.",
                channelPointSetEnabled.EnablePoint ? "Enabled" : "Disable",
                channelPointSetEnabled.Text);

            var channelPoints = await twitchService.GetChannelPointRewards(true);
            foreach (var channelPoint in channelPoints)
            {
                if(channelPoint.Title.Equals(channelPointSetEnabled.Text, StringComparison.OrdinalIgnoreCase))
                {
                    if (channelPoint.IsEnabled != channelPointSetEnabled.EnablePoint)
                    {
                        UpdateCustomRewardRequest updateRequest = new()
                        {
                            IsEnabled = channelPointSetEnabled.EnablePoint
                        };
                        await twitchService.UpdateChannelPointReward(channelPoint.Id, updateRequest);
                        logger.LogInformation("{state} {title} channel point reward.",
                            channelPointSetEnabled.EnablePoint ? "Enabled" : "Disable",
                            channelPointSetEnabled.Text);
                    }
                    else
                    {
                        logger.LogInformation("{title} channel point reward is already in the desired state.",
                            channelPointSetEnabled.Text);
                    }
                    break;
                }
            }
        }
    }
}
