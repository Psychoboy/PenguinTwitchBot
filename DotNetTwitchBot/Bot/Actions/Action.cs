using DotNetTwitchBot.Bot.Actions.SubActions;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Queues;
using DotNetTwitchBot.Bot.WebSocketEvents;
using DotNetTwitchBot.Extensions;
using DotNetTwitchBot.Repository;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions
{
    public class Action(
        ILogger<Action> logger, 
        IServiceScopeFactory scopeFactory, 
        IServiceBackbone serviceBackbone,
        IServiceProvider serviceProvider) : IAction
    {
        public async Task<ActionType> AddAction(ActionType action)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.Actions.Add(action);
            db.SaveChanges();
            return action;
        }

        public async Task EnqueueAction(ConcurrentDictionary<string, string> variables, ActionType action, Guid? parentLogId = null, int? parentSubActionIndex = null)
        {
            if(action.OnlineOnly && !serviceBackbone.IsOnline)
            {
                logger.LogInformation("Action {ActionName} is set to only run when streamer is online, but streamer is currently offline, skipping", action.Name);
                return;
            }

            if(action.Enabled == false)
            {
                logger.LogInformation("Action {ActionName} is disabled, skipping", action.Name);
                return;
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var queueManager = scope.ServiceProvider.GetRequiredService<IQueueManager>();
            var queue = await queueManager.GetQueueAsync(action.QueueName);
            await queue.EnqueueAsync(action, variables, parentLogId, parentSubActionIndex);
            logger.LogDebug("Action {ActionName} enqueued to {QueueName}", action.Name, action.QueueName);
        }

        public async Task RunAction(ConcurrentDictionary<string, string> variables, ActionType action)
        {
            if (!action.Enabled)
            {
                logger.LogInformation("Action {action.Name} was disabled so skipping", action.Name);
                return;
            }



            var enabledSubActions = action.SubActions.Where(x => x.Enabled == true).ToList();

            try
            {
                if (action.RandomAction)
                {
                    var subAction = enabledSubActions.RandomElementOrDefault();
                    if (subAction != null)
                    {
                        await RunSubAction(subAction, variables);
                        return;
                    }
                }

                if (action.ConcurrentAction)
                {
                    var subActions = enabledSubActions;
                    var tasks = subActions.Select(item => RunSubAction(item, variables));
                    await Task.WhenAll(tasks);
                    return;
                }

                foreach (var subAction in enabledSubActions.OrderBy(subAction => subAction.Index))
                {
                    await RunSubAction(subAction, variables);
                }
            } catch (BreakException)
            {
                // Do nothing, just break out of the action execution
            }

        }

        private async Task RunSubAction(SubActionType subAction, ConcurrentDictionary<string, string> variables)
        {
            // Use the current scope's service provider instead of creating a new scope
            // This ensures the SubActionHandlerFactory gets the same ISubActionExecutionContextAccessor
            // instance that was set up in ActionQueue.ExecuteActionAsync
            // 
            // NOTE: When action.ConcurrentAction is true, multiple sub-actions run in parallel
            // within the same DI scope. This is safe for most services (stateless, singleton, or transient).
            // However, if a SubAction handler injects scoped services with mutable state (e.g., DbContext),
            // concurrent access could cause issues. The trade-off was made to ensure context accessor works
            // correctly. If concurrent DbContext usage becomes a problem, consider using DbContextFactory
            // or explicitly creating per-subaction scopes with context propagation.
            var factory = serviceProvider.GetRequiredService<SubActionHandlerFactory>();
            await factory.ExecuteAsync(subAction, variables);
        }
    }
}
