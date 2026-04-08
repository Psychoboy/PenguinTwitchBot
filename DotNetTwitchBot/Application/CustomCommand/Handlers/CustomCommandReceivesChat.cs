using DotNetTwitchBot.Application.ChatMessage.Notifications;

namespace DotNetTwitchBot.Application.CustomCommand.Handlers
{
    public class CustomCommandReceivesChat(Bot.Commands.Custom.CustomCommand customCommand) : Application.Notifications.INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            return customCommand.ReceivedChatMessage(notification.EventArgs);
        }
    }
}
