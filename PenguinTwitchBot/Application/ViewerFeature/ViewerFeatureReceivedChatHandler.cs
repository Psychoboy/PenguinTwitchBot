using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Bot.Commands.Features;

namespace PenguinTwitchBot.Application.ViewerFeature
{
    public class ViewerFeatureReceivedChatHandler(IViewerFeature viewerFeature) : Application.Notifications.INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            return viewerFeature.OnChatMessage(notification.EventArgs);
        }
    }
}
