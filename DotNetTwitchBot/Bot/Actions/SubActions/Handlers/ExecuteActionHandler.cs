using DotNetTwitchBot.Bot.Actions.SubActions.Types;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ExecuteActionHandler(IActionManagementService actionService, IAction action) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ExecuteAction;

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not ExecuteActionType executeAction)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type provided to ExecuteActionHandler: {SubActionType}", subAction.GetType().Name);
            }

            var actionId = executeAction.ActionId;
            if(!actionId.HasValue || actionId.Value == 0)
            {
                throw new SubActionHandlerException(subAction, "Invalid action ID provided to ExecuteActionHandler: {ActionId}", actionId.HasValue ? actionId : "" );
            }

            // Execute the action using the parsed actionId
            var actionItem = await actionService.GetActionByIdAsync(actionId.Value);
            if(actionItem == null)
            {
                throw new SubActionHandlerException(subAction, "No action found with ID: {ActionId}", actionId);
            }
            await action.EnqueueAction(new Dictionary<string, string>(variables), actionItem);
        }
    }
}
