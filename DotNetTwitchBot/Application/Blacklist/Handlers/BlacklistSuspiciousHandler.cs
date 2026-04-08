using DotNetTwitchBot.Application.ChatMessage.Notifications;

namespace DotNetTwitchBot.Application.Blacklist.Handlers
{
    public class BlacklistSuspiciousHandler(Bot.Commands.Moderation.Blacklist blacklist) : Application.Notifications.INotificationHandler<ReceivedSuspiciousChatMessage>
    {
        public Task Handle(ReceivedSuspiciousChatMessage notification, CancellationToken cancellationToken)
        {
            return blacklist.ChatMessage(notification.EventArgs);
        }
    }
}
