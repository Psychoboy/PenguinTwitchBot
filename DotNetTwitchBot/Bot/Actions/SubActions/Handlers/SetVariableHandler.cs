using DotNetTwitchBot.Bot.Actions.SubActions.Types;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class SetVariableHandler(ILogger<SetVariableHandler> logger) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.SetVariable;

        public Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if(subAction is not SetVariableType setVariableType)
            {
                logger.LogError("Invalid sub action type for SetVariableHandler: {Type}", subAction.GetType().Name);
                return Task.CompletedTask;
            }

            setVariableType.Value = VariableReplacer.ReplaceVariables(setVariableType.Value, variables);
            variables[$"{setVariableType.Text}"] = setVariableType.Value;

            return Task.CompletedTask;
        }
    }
}
