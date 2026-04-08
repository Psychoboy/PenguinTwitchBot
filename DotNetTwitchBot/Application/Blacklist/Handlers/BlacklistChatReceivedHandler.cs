
using DotNetTwitchBot.Application.ChatMessage.Notifications;

namespace DotNetTwitchBot.Application.Blacklist.Handlers
{
    public class BlacklistChatReceivedHandler(Bot.Commands.Moderation.Blacklist blacklist) : Application.Notifications.INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            return blacklist.ChatMessage(notification.EventArgs);
        }
    }

    public class BlacklistSuspiciousChatReceivedHandler(Bot.Commands.Moderation.Blacklist blacklist) : Application.Notifications.INotificationHandler<ReceivedSuspiciousChatMessage>
    {
        public Task Handle(ReceivedSuspiciousChatMessage notification, CancellationToken cancellationToken)
        {
            return blacklist.ChatMessage(notification.EventArgs);
        }
    }
}
