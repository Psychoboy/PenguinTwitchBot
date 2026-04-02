using DotNetTwitchBot.Bot.Actions.SubActions;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Actions.SubActions;
using DotNetTwitchBot.Bot.Queues;
using DotNetTwitchBot.Extensions;
using DotNetTwitchBot.Repository;
using MediatR;

namespace DotNetTwitchBot.Bot.Actions
{
    public class Action (ILogger<Action> logger, IServiceScopeFactory scopeFactory)
    {
        public async Task<Models.Actions.ActionType> AddAction(Models.Actions.ActionType action)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.Actions.Add(action);
            db.SaveChanges();
            return action;
        }

        public async Task EnqueueAction(Dictionary<string, string> variables, Models.Actions.ActionType action)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var queueManager = scope.ServiceProvider.GetRequiredService<IQueueManager>();
            var queue = await queueManager.GetQueueAsync(action.QueueName);
            await queue.EnqueueAsync(action, variables);
            logger.LogDebug("Action {ActionName} enqueued to {QueueName}", action.Name, action.QueueName);
        }

        public async Task RunAction(Dictionary<string, string> variables, Models.Actions.ActionType action)
        {
            if(!action.Enabled)
            {
                logger.LogInformation("Action {action.Name} was disabled so skipping", action.Name);
                return;
            }

            if (action.RandomAction)
            {
                var subAction = action.SubActions.RandomElementOrDefault();
                if (subAction != null)
                {
                    await RunSubAction(subAction, variables);
                    return;
                }
            }

            if(action.ConcurrentAction)
            {
                var subActions = action.SubActions;
                var tasks = subActions.Select(item => RunSubAction(item, variables));
                await Task.WhenAll(tasks);
                return;
            }

            action.SubActions.OrderBy(subAction => subAction.Index).ToList().ForEach(subAction => RunSubAction(subAction, variables).Wait());
        }

        private async Task RunSubAction(SubActionType subAction, Dictionary<string, string> variables)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<SubActionHandlerFactory>();
            await factory.ExecuteAsync(subAction, variables);
        }
    }
}
