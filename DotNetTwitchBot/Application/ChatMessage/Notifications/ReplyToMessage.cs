using MediatR;

namespace DotNetTwitchBot.Application.ChatMessage.Notifications
{
    public class ReplyToMessage(string name, string messageId, string message, PlatformType platform) : INotification
    {
        public string Name { get; } = name;
        public string MessageId { get; } = messageId;
        public string Message { get; } = message;
        public PlatformType Platform { get; } = platform;
    }
}
