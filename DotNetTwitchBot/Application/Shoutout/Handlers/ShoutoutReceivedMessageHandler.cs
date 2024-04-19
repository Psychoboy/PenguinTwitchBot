using DotNetTwitchBot.Application.ChatMessage.Notifications;
using DotNetTwitchBot.Bot.Commands.Misc;
using MediatR;

namespace DotNetTwitchBot.Application.Shoutout.Handlers
{
    public class ShoutoutReceivedMessageHandler(ShoutoutSystem shoutoutSystem) : INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            return shoutoutSystem.OnChatMessage(notification.EventArgs);
        }
    }
}
