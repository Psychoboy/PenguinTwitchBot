using MediatR;

namespace DotNetTwitchBot.Application.ChatMessage.Notification
{
    public class SendBotMessage(string message, bool sourceOnly) : INotification
    {
        public string Message { get; } = message;
        public bool SourceOnly { get; } = sourceOnly;
    }
}
