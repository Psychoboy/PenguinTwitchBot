using PenguinTwitchBot.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.TwitchApi.Models.ChannelPoints;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ChannelPointSetEnabledStateHandler(
        ILogger<ChannelPointSetEnabledStateHandler> logger,
        ITwitchService twitchService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ChannelPointSetEnabledState;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not ChannelPointSetEnabledStateType channelPointSetEnabled)
            {
                throw new SubActionHandlerException(subAction, $"Unexpected sub action type: {subAction.GetType().Name}");
            }

            logger.LogInformation("Trying to {state} {title} channel point reward.",
                channelPointSetEnabled.EnablePoint ? "Enabled" : "Disable",
                channelPointSetEnabled.Text);

            var channelPoints = await twitchService.GetChannelPointRewards(true);
            foreach (var channelPoint in channelPoints)
            {
                if (channelPoint.Title.Equals(channelPointSetEnabled.Text, StringComparison.OrdinalIgnoreCase))
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