using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ExecuteActionHandler(
        IActionManagementService actionService, 
        IAction action,
        ISubActionExecutionContextAccessor contextAccessor) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ExecuteAction;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables)
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

                int currentIndex = contextAccessor.CurrentSubActionIndex;
                context.LogMessage(currentIndex, $"Enqueueing action: {actionItem.Name} to queue: {actionItem.QueueName}");

                // Pass parent context info so the child action can be linked
                // Use the CurrentSubActionIndex from the accessor which was set by SubActionHandlerFactory
                await action.EnqueueAction(
                    new ConcurrentDictionary<string, string>(variables), 
                    actionItem,
                    parentLogId: context.ActionLogId,
                    parentSubActionIndex: currentIndex);

                context.LogMessage(currentIndex, $"Action enqueued successfully. Child action will be linked when it starts.");

            }
            else
            {
                // No context available, just enqueue normally
                await action.EnqueueAction(new ConcurrentDictionary<string, string>(variables), actionItem);
            }
        }
    }
}
