using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions
{
    public class SubActionHandlerFactory(
        IEnumerable<ISubActionHandler> handlers, 
        ILogger<SubActionHandlerFactory> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        private readonly Dictionary<SubActionTypes, ISubActionHandler> _handlers = handlers.ToDictionary(h => h.SupportedType);

        public async Task ExecuteAsync(SubActionType subAction, int subActionIndex, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null)
        {
            if (_handlers.TryGetValue(subAction.SubActionTypes, out var handler))
            {
                if(!subAction.Enabled)
                {
                    logger.LogInformation("Sub action {subAction.Text} was disabled so skipping", subAction.Text);
                    return;
                }

                var subActionTypeName = subAction.SubActionTypes.ToString();
                var description = !string.IsNullOrEmpty(subAction.Text) ? subAction.Text : null;

                if (context != null)
                {
                    // Use explicit index to avoid race conditions in concurrent execution
                    context.BeginSubAction(subActionIndex, subActionTypeName, description);
                }
                else
                {
                    logger.LogWarning("Executing SubAction {SubActionType} without execution context - logs will not be recorded", 
                        subActionTypeName);
                }

                try
                {
                    // Always create a new scope to prevent DbContext sharing in concurrent scenarios
                    // This ensures thread-safety for all scoped services at negligible performance cost
                    await using var scope = serviceScopeFactory.CreateAsyncScope();

                    // Resolve handler from new scope, fall back to original if not available and log it as it should not happen
                    // since all handlers should automatically be registered. This is to ensure that if a handler has any scoped dependencies,
                    // they are properly isolated in concurrent execution scenarios.
                    var handlerType = handler.GetType();
                    if (scope.ServiceProvider.GetService(handlerType) is not ISubActionHandler scopedHandler)
                    {
                        logger.LogWarning("Unable to resolve scoped handler of concrete type {HandlerType} for SubActionType {SubActionType}. Falling back to the original handler instance, " +
                            "which may bypass intended scoped-service concurrency isolation. Ensure the concrete handler type is registered with DI.", 
                            handlerType.FullName, subAction.SubActionTypes);
                        scopedHandler = handler;
                    }

                    // Pass the context and explicit index to the handler
                    await scopedHandler.ExecuteAsync(subAction, variables, context, subActionIndex);
                    context?.CompleteSubAction(subActionIndex);
                }
                catch (BreakException)
                {
                    context?.LogMessage(subActionIndex, "Break caught, stopping further execution of this action.");
                    context?.CompleteSubAction(subActionIndex);
                    throw;
                }
                catch (Exception ex)
                {
                    context?.FailSubAction(subActionIndex, ex.Message);
                    throw;
                }
            }
            else
            {
                logger.LogWarning("No handler found for SubActionType: {SubActionType}", subAction.SubActionTypes);
            }
        }
    }
}
