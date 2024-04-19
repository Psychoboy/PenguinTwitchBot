using DotNetTwitchBot.Application.ChatMessage.Notifications;
using DotNetTwitchBot.Bot.Commands.Features;
using MediatR;

namespace DotNetTwitchBot.Application.LoyaltyFeature.Handlers
{
    public class LoyaltyChatMessageHandler(ILoyaltyFeature loyaltyFeature) : INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            return loyaltyFeature.OnChatMessage(notification.EventArgs);
        }
    }
}
