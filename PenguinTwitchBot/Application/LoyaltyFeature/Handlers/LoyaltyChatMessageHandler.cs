using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Bot.Commands.Features;

namespace PenguinTwitchBot.Application.LoyaltyFeature.Handlers
{
    public class LoyaltyChatMessageHandler(ILoyaltyFeature loyaltyFeature) : Application.Notifications.INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            return loyaltyFeature.OnChatMessage(notification.EventArgs);
        }
    }
}
