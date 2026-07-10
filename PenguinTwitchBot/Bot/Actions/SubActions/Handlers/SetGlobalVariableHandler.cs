using System.Collections.Concurrent;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class SetGlobalVariableHandler(ILogger<SetGlobalVariableHandler> logger, IUnitOfWork unitOfWork) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.SetGlobalVariable;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not SetGlobalVariableType setGlobalVariableType)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for SetGlobalVariableHandler: {Type}", subAction.GetType().Name);
            }

            var variableName = setGlobalVariableType.Text?.Trim();
            if (string.IsNullOrWhiteSpace(variableName))
            {
                throw new SubActionUserFacingException(subAction, "Global variable name is required.");
            }

            var value = VariableReplacer.ReplaceVariables(setGlobalVariableType.Value, variables);
            await unitOfWork.GlobalVariables.UpsertAsync(variableName, value);

            logger.LogInformation("Stored global variable {VariableName}", variableName);
            context?.LogMessage(subActionIndex, $"Stored global variable '{variableName}'.");
        }
    }
}