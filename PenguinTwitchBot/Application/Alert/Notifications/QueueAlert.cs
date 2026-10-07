using PenguinTwitchBot.Bot.Notifications;

namespace PenguinTwitchBot.Application.Alert.Notification
{
    public class QueueAlert(string alert, string topic = WsTopics.Alerts) : Application.Notifications.INotification
    {
        public string Alert { get; } = alert;
        public string Topic { get; } = topic;
    }
}
