using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Notifications;
using MediatR;

namespace DotNetTwitchBot.Application.Alert.Handlers
{
    public class QueueAlertHandler(IWebSocketMessenger webSocketMessenger) : INotificationHandler<QueueAlert>
    {
        public Task Handle(QueueAlert request, CancellationToken cancellationToken)
        {
            webSocketMessenger.AddToQueue(request.Alert);
            return Task.CompletedTask;
        }
    }
}
