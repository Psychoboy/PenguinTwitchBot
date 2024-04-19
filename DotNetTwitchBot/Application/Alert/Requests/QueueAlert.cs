using MediatR;

namespace DotNetTwitchBot.Application.Alert.Notification
{
    public class QueueAlert(string alert) : IRequest
    {
        public string Alert { get; } = alert;
    }
}
