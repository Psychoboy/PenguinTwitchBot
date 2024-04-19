using DotNetTwitchBot.Application.ChatMessage.Notifications;
using MediatR;

namespace DotNetTwitchBot.Application.CustomCommand.Handlers
{
    public class CustomCommandReceivesChat(Bot.Commands.Custom.CustomCommand customCommand) : INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            return customCommand.ReceivedChatMessage(notification.EventArgs);
        }
    }
}
