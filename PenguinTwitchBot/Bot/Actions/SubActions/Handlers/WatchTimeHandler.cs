using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Bot.Commands.Features;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class WatchTimeHandler(ILoyaltyFeature loyaltyFeature) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.WatchTime;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
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

