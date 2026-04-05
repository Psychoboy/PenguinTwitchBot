using DotNetTwitchBot.Bot.Actions.SubActions.Types;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ExecuteActionHandler(ILogger<ExecuteActionHandler> logger, IActionManagementService actionService, IAction action) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ExecuteAction;

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not ExecuteActionType executeAction)
            {
                logger.LogError("Invalid sub action type provided to ExecuteActionHandler: {SubActionType}", subAction.GetType().Name);
                return;
            }

            var actionIdString = executeAction.ActionId;
            if(!int.TryParse(actionIdString, out var actionId))
            {
                logger.LogError("Invalid action ID provided to ExecuteActionHandler: {ActionId}", actionIdString);
                return;
            }

            // Execute the action using the parsed actionId
            var actionItem = await actionService.GetActionByIdAsync(actionId);
            if(actionItem == null)
            {
                logger.LogError("No action found with ID: {ActionId}", actionId);
                return;
            }
            await action.EnqueueAction(variables, actionItem);
        }
    }
}
