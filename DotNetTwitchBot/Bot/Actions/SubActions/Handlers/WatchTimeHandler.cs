using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Commands.Features;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class WatchTimeHandler(ILogger<WatchTimeHandler> logger, ILoyaltyFeature loyaltyFeature) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.WatchTime;

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if(subAction is not WatchTimeType watchTimeType)
            {
                logger.LogError("Invalid sub action type for WatchTimeHandler: {SubActionType}", subAction.GetType().Name);
                return;
            }

            watchTimeType.Text = VariableReplacer.ReplaceVariables(watchTimeType.Text, variables);
            variables["watch_time"] = await loyaltyFeature.GetViewerWatchTime(watchTimeType.Text);
        }
    }
}
