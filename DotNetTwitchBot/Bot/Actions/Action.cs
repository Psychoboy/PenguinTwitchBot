using DotNetTwitchBot.Bot.Actions.SubActions;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Queues;
using DotNetTwitchBot.Extensions;
using DotNetTwitchBot.Repository;
using MediatR;

namespace DotNetTwitchBot.Bot.Actions
{
    public class Action(ILogger<Action> logger, IServiceScopeFactory scopeFactory) : IAction
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
            await using var scope = scopeFactory.CreateAsyncScope();
            var queueManager = scope.ServiceProvider.GetRequiredService<IQueueManager>();
            var queue = await queueManager.GetQueueAsync(action.QueueName);
            if(action.Id.HasValue && action.Id > 0 && variables.TryGetValue("ExecutedActions", out var executedActions))
            {
                if(executedActions.Split(',').Contains(action.Id.Value.ToString()))
                {
                    logger.LogWarning("Action {ActionName} with Id {ActionId} has already been executed in this chain of actions, skipping to prevent infinite loop", action.Name, action.Id);
                    return;
                }
                variables["ExecutedActions"] = $"{executedActions},{action.Id}";
            }
            else if (action.Id.HasValue && action.Id > 0)
            {
                variables["ExecutedActions"] = action.Id.HasValue ? action.Id.Value.ToString() : string.Empty;
            }
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
        }

        private async Task RunSubAction(SubActionType subAction, Dictionary<string, string> variables)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<SubActionHandlerFactory>();
            await factory.ExecuteAsync(subAction, variables);
        }
    }
}
