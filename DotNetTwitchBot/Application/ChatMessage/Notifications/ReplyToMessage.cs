using MediatR;

namespace DotNetTwitchBot.Application.ChatMessage.Notifications
{
    public class ReplyToMessage(string messageId, string message) : INotification
    {
        public string MessageId { get; } = messageId;
        public string Message { get; } = message;
    }
}
