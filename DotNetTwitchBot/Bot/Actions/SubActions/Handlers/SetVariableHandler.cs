using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class SetVariableHandler() : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.SetVariable;

        public Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null)
        {
            if(subAction is not SetVariableType setVariableType)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for SetVariableHandler: {Type}", subAction.GetType().Name);
            }

            setVariableType.Value = VariableReplacer.ReplaceVariables(setVariableType.Value, variables);
            variables[$"{setVariableType.Text}"] = setVariableType.Value;

            return Task.CompletedTask;
        }
    }
}
