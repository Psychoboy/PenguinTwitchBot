using MediatR;

namespace DotNetTwitchBot.Application.ChatMessage.Notification
{
    public class SendBotMessage(string message) : INotification
    {
        public string Message { get; } = message;
    }
}
