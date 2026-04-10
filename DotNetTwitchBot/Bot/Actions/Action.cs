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
        IServiceBackbone serviceBackbone) : IAction
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

        public async Task RunAction(ConcurrentDictionary<string, string> variables, ActionType action, ActionExecutionContext? context = null)
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
                        await RunSubAction(subAction, subAction.Index, variables, context);
                        return;
                    }
                }

                if (action.ConcurrentAction)
                {
                    var subActions = enabledSubActions;
                    var tasks = subActions.Select(item => RunSubAction(item, item.Index, variables, context));
                    await Task.WhenAll(tasks);
                    return;
                }

                foreach (var subAction in enabledSubActions.OrderBy(subAction => subAction.Index))
                {
                    await RunSubAction(subAction, subAction.Index, variables, context);
                }
            } catch (BreakException)
            {
                // Do nothing, just break out of the action execution
            }

        }

        private async Task RunSubAction(SubActionType subAction, int subActionIndex, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<SubActionHandlerFactory>();
            await factory.ExecuteAsync(subAction, subActionIndex, variables, context);
        }
    }
}
