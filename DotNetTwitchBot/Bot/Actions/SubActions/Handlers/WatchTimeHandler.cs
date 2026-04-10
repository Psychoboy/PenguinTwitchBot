using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;
using DotNetTwitchBot.Bot.Commands.Features;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class WatchTimeHandler(ILoyaltyFeature loyaltyFeature) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.WatchTime;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null)
        {
            if(subAction is not WatchTimeType watchTimeType)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for WatchTimeHandler: {SubActionType}", subAction.GetType().Name);
            }

            watchTimeType.Text = VariableReplacer.ReplaceVariables(watchTimeType.Text, variables);
            variables["watch_time"] = await loyaltyFeature.GetViewerWatchTime(watchTimeType.Text);
        }
    }
}
