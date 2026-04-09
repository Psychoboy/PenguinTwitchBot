using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions
{
    public class SubActionHandlerFactory(
        IEnumerable<ISubActionHandler> handlers, 
        ILogger<SubActionHandlerFactory> logger,
        IServiceProvider serviceProvider,
        IServiceScopeFactory serviceScopeFactory)
    {
        private readonly Dictionary<SubActionTypes, ISubActionHandler> _handlers = handlers.ToDictionary(h => h.SupportedType);

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables)
        {
            if (_handlers.TryGetValue(subAction.SubActionTypes, out var handler))
            {
                if(!subAction.Enabled)
                {
                    logger.LogInformation("Sub action {subAction.Text} was disabled so skipping", subAction.Text);
                    return;
                }

                // Resolve the context accessor from the current scope instead of constructor injection
                var contextAccessor = serviceProvider.GetService<ISubActionExecutionContextAccessor>();
                var context = contextAccessor?.ExecutionContext;
                var subActionTypeName = subAction.SubActionTypes.ToString();
                var description = !string.IsNullOrEmpty(subAction.Text) ? subAction.Text : null;

                int subActionIndex = -1;
                if (context != null)
                {
                    logger.LogDebug("Executing SubAction {SubActionType} with context (LogId: {LogId})", 
                        subActionTypeName, context.ActionLogId);
                    subActionIndex = context.BeginSubAction(subActionTypeName, description);

                    // Store the index in the accessor so handlers can access it if needed
                    // This is safe for concurrent execution because each parallel task gets its own ExecutionContext flow
                    if (contextAccessor != null)
                    {
                        contextAccessor.CurrentSubActionIndex = subActionIndex;
                    }
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

                    // Copy the execution context to the new scope's context accessor
                    var scopedContextAccessor = scope.ServiceProvider.GetService<ISubActionExecutionContextAccessor>();
                    if (scopedContextAccessor != null && contextAccessor != null)
                    {
                        scopedContextAccessor.ExecutionContext = contextAccessor.ExecutionContext;
                        scopedContextAccessor.CurrentSubActionIndex = subActionIndex;
                    }

                    // Resolve handler from new scope, fall back to original if not available (e.g., in tests)
                    var handlerType = handler.GetType();
                    var scopedHandler = scope.ServiceProvider.GetService(handlerType) as ISubActionHandler ?? handler;

                    await scopedHandler.ExecuteAsync(subAction, variables);
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
