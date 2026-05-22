using PenguinTwitchBot.Application.Alert.Notification;
using PenguinTwitchBot.Bot.Notifications;

namespace PenguinTwitchBot.Application.Alert.Handlers
{
    public class QueueAlertHandler(IWebSocketMessenger webSocketMessenger) : Application.Notifications.INotificationHandler<QueueAlert>
    {
        public async Task Handle(QueueAlert request, CancellationToken cancellationToken)
        {
            await webSocketMessenger.AddToQueue(request.Alert);
        }
    }
}
