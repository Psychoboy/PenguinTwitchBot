using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ExecuteActionHandler(
        IActionManagementService actionService, 
        IAction action,
        ISubActionExecutionContextAccessor contextAccessor) : ISubActionHandler
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

            var context = contextAccessor.ExecutionContext;

            if (context != null)
            {
                // Create a nested context for the executed action
                var nestedContext = context.CreateNestedContext();
                nestedContext.BeginSubAction("ExecuteAction", $"Action: {actionItem.Name}");

                try
                {
                    nestedContext.LogMessage($"Enqueueing action: {actionItem.Name} to queue: {actionItem.QueueName}");
                    await action.EnqueueAction(new Dictionary<string, string>(variables), actionItem);
                    nestedContext.LogMessage($"Action enqueued successfully");
                    nestedContext.CompleteSubAction();
                }
                catch (Exception ex)
                {
                    nestedContext.FailSubAction(ex.Message);
                    throw;
                }
            }
            else
            {
                // No context available, just enqueue normally
                await action.EnqueueAction(new Dictionary<string, string>(variables), actionItem);
            }
        }
    }
}
