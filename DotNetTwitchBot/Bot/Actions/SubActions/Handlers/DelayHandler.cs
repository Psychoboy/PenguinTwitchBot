using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{

    public class DelayHandler(ISubActionExecutionContextAccessor contextAccessor) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.Delay;

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if(subAction is not DelayType delaySubAction)
                throw new SubActionHandlerException(subAction, $"Expected {nameof(DelayType)} but got {subAction.GetType().Name}");

            var context = contextAccessor.ExecutionContext;
            var durationStr = VariableReplacer.ReplaceVariables(delaySubAction.Duration, variables);

            if (int.TryParse(durationStr, out var duration))
            {
                context?.LogMessage($"Delaying for {duration}ms");
                await Task.Delay(duration);
            }
            else
            {
                context?.LogMessage($"Invalid duration value: {durationStr}");
            }
        }
    }
}
