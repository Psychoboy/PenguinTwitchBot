using DotNetTwitchBot.Application.ChatMessage.Notifications;
using DotNetTwitchBot.Bot.Commands.Features;

namespace DotNetTwitchBot.Application.LoyaltyFeature.Handlers
{
    public class LoyaltyChatMessageHandler(ILoyaltyFeature loyaltyFeature) : Application.Notifications.INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            return loyaltyFeature.OnChatMessage(notification.EventArgs);
        }
    }
}
