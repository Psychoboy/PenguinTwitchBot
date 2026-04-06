using DotNetTwitchBot.Bot.Actions.SubActions;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Queues;
using DotNetTwitchBot.Bot.WebSocketEvents;
using DotNetTwitchBot.Extensions;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Actions
{
    public class Action(ILogger<Action> logger, IServiceScopeFactory scopeFactory, IServiceBackbone serviceBackbone) : IAction
    {
        public async Task<ActionType> AddAction(ActionType action)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.Actions.Add(action);
            db.SaveChanges();
            return action;
        }

        public async Task EnqueueAction(Dictionary<string, string> variables, ActionType action)
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
            await queue.EnqueueAsync(action, variables);
            logger.LogDebug("Action {ActionName} enqueued to {QueueName}", action.Name, action.QueueName);
        }

        public async Task RunAction(Dictionary<string, string> variables, ActionType action)
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

        private async Task RunSubAction(SubActionType subAction, Dictionary<string, string> variables)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<SubActionHandlerFactory>();
            await factory.ExecuteAsync(subAction, variables);
        }
    }
}
