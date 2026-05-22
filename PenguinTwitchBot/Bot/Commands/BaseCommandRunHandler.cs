using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Commands
{
    public class BaseCommandRunHandler(
        ICommandHandler commandHandler, 
        IServiceScopeFactory serviceScopeFactory, 
        ILogger<BaseCommandRunHandler> logger) : Application.Notifications.INotificationHandler<RunCommandNotification>
    {
        public async Task Handle(RunCommandNotification notification, CancellationToken cancellationToken)
        {
            var eventArgs = notification.EventArgs;
            if(eventArgs == null)
            {
                logger.LogError("RunCommandNotification EventArgs is null");
                return;
            }

            var commandService = commandHandler.GetCommand(eventArgs.Command);
            if (commandService != null && commandService.CommandProperties.Disabled == false && CommandHandler.CheckIfAllowedInSharedChat(eventArgs, commandService.CommandProperties))
            {
                var action = new ActionType
                {
                    Name = commandService.CommandProperties.CommandName,
                    Group = "Default Command",
                    Enabled = true,
                    RandomAction = false,
                    ConcurrentAction = false,
                    OnlineOnly = false,
                    QueueName = QueueManager.DefaultQueueName
                };

                var subAction = new RuntimeDefaultCommandType();
                action.SubActions = new List<SubActionType> { subAction };
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var actionService = scope.ServiceProvider.GetRequiredService<IAction>();
                await actionService.EnqueueAction(CommandEventArgsConverter.ToDictionary(eventArgs), action);
            }
        }
    }
}
