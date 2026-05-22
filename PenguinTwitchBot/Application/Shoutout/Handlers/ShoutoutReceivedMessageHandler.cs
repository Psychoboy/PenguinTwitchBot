using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Bot.Commands.Misc;

namespace PenguinTwitchBot.Application.Shoutout.Handlers
{
    public class ShoutoutReceivedMessageHandler(ShoutoutSystem shoutoutSystem) : Application.Notifications.INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            return shoutoutSystem.OnChatMessage(notification.EventArgs);
        }
    }
}
