using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Notifications;
using MediatR;

namespace DotNetTwitchBot.Application.Alert.Handlers
{
    public class QueueAlertHandler(IWebSocketMessenger webSocketMessenger) : INotificationHandler<QueueAlert>
    {
        public async Task Handle(QueueAlert request, CancellationToken cancellationToken)
        {
            await webSocketMessenger.AddToQueue(request.Alert);
        }
    }
}
