using DotNetTwitchBot.Application.ChatMessage.Notifications;
using DotNetTwitchBot.Bot.Commands.Features;
using MediatR;

namespace DotNetTwitchBot.Application.ViewerFeature
{
    public class ViewerFeatureReceivedChatHandler(IViewerFeature viewerFeature) : INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            return viewerFeature.OnChatMessage(notification.EventArgs);
        }
    }
}
