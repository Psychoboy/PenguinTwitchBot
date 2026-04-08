using DotNetTwitchBot.Application.Notifications;

namespace DotNetTwitchBot.Application.Alert.Notification
{
    public class QueueAlert(string alert) : INotification
    {
        public string Alert { get; } = alert;
    }
}
