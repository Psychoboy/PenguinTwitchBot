using DotNetTwitchBot.Application.ChatMessage.Notifications;
using DotNetTwitchBot.Bot.Commands.Features;

namespace DotNetTwitchBot.Application.ViewerFeature
{
    public class ViewerFeatureReceivedChatHandler(IViewerFeature viewerFeature) : Application.Notifications.INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            return viewerFeature.OnChatMessage(notification.EventArgs);
        }
    }
}
