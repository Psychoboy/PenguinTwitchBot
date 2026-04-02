using DotNetTwitchBot.Bot.Actions;
using DotNetTwitchBot.Bot.Utilities;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Actions
{
    public class ActionCommandHandler(IActionManagementService actionManagement, IAction actionService) : INotificationHandler<RunCommandNotification>
    {
        public async Task Handle(RunCommandNotification notification, CancellationToken cancellationToken)
        {
            if (notification.EventArgs == null || string.IsNullOrWhiteSpace(notification.EventArgs.Command))
                return;
            var actions = await actionManagement.GetActionsByTriggerTypeAndNameAsync(Models.Actions.Triggers.TriggerTypes.Command, "!" + notification.EventArgs.Command);

            var dictionary = CommandEventArgsConverter.ToDictionary(notification.EventArgs);

            foreach (var action in actions)
            {
                await actionService.EnqueueAction(dictionary, action);
            }
        }
    }
}
