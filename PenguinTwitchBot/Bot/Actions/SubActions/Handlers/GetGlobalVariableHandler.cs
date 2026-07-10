using System.Collections.Concurrent;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class GetGlobalVariableHandler(ILogger<GetGlobalVariableHandler> logger, IUnitOfWork unitOfWork) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.GetGlobalVariable;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not GetGlobalVariableType getGlobalVariableType)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for GetGlobalVariableHandler: {Type}", subAction.GetType().Name);
            }

            var variableName = getGlobalVariableType.Text?.Trim();
            var targetVariableName = getGlobalVariableType.TargetVariableName?.Trim();

            if (string.IsNullOrWhiteSpace(variableName))
            {
                throw new SubActionUserFacingException(subAction, "Global variable name is required.");
            }

            if (string.IsNullOrWhiteSpace(targetVariableName))
            {
                throw new SubActionUserFacingException(subAction, "Local variable name is required.");
            }

            var globalVariable = await unitOfWork.GlobalVariables.GetByNameAsync(variableName);
            var value = globalVariable?.Value ?? string.Empty;

            variables[targetVariableName] = value;

            if (globalVariable == null)
            {
                logger.LogInformation("Global variable {VariableName} was not found. Stored an empty value in local variable {TargetVariableName}.", variableName, targetVariableName);
                context?.LogMessage(subActionIndex, $"Global variable '{variableName}' was not found; '{targetVariableName}' was set to an empty value.");
            }
            else
            {
                logger.LogInformation("Loaded global variable {VariableName} into local variable {TargetVariableName}", variableName, targetVariableName);
                context?.LogMessage(subActionIndex, $"Loaded global variable '{variableName}' into '{targetVariableName}'.");
            }
        }
    }
}