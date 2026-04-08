using DotNetTwitchBot.Application.ChatMessage.Notifications;
using DotNetTwitchBot.Bot.Commands.Misc;

namespace DotNetTwitchBot.Application.Shoutout.Handlers
{
    public class ShoutoutReceivedMessageHandler(ShoutoutSystem shoutoutSystem) : Application.Notifications.INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            return shoutoutSystem.OnChatMessage(notification.EventArgs);
        }
    }
}
