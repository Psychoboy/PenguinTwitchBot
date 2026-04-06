using DotNetTwitchBot.Bot.Actions.SubActions.Types;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    
    public class DelayHandler : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.Delay;

        public Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if(subAction is not DelayType delaySubAction)
                throw new ArgumentException($"Expected {nameof(DelayType)} but got {subAction.GetType().Name}");

            var durationStr = VariableReplacer.ReplaceVariables(delaySubAction.Duration, variables);
            if (int.TryParse(durationStr, out var duration))
            {
                Thread.Sleep(duration);
            }
            return Task.CompletedTask;
        }
    }
}
