using MediatR;

namespace DotNetTwitchBot.Application.Alert.Notification
{
    public class QueueAlert(string alert) : INotification
    {
        public string Alert { get; } = alert;
    }
}
