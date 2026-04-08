
namespace DotNetTwitchBot.Application.Alert.Notification
{
    public class QueueAlert(string alert) : Application.Notifications.INotification
    {
        public string Alert { get; } = alert;
    }
}
