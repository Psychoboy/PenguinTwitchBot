using DotNetTwitchBot.Application.ChatMessage.Notifications;
using DotNetTwitchBot.Application.Notifications;

namespace DotNetTwitchBot.Application.Blacklist.Handlers
{
    public class BlacklistSuspiciousHandler(Bot.Commands.Moderation.Blacklist blacklist) : INotificationHandler<ReceivedSuspiciousChatMessage>
    {
        public Task Handle(ReceivedSuspiciousChatMessage notification, CancellationToken cancellationToken)
        {
            return blacklist.ChatMessage(notification.EventArgs);
        }
    }
}
