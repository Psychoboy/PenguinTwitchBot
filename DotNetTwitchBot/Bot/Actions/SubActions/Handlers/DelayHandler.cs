using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{

    public class DelayHandler() : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.Delay;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null)
        {
            if(subAction is not DelayType delaySubAction)
                throw new SubActionHandlerException(subAction, $"Expected {nameof(DelayType)} but got {subAction.GetType().Name}");

            var durationStr = VariableReplacer.ReplaceVariables(delaySubAction.Duration, variables);

            if (int.TryParse(durationStr, out var duration))
            {
                context?.LogMessage(context.CurrentSubActionIndex, $"Delaying for {duration}ms");
                await Task.Delay(duration);
            }
            else
            {
                context?.LogMessage(context.CurrentSubActionIndex, $"Invalid duration value: {durationStr}");
            }
        }
    }
}
