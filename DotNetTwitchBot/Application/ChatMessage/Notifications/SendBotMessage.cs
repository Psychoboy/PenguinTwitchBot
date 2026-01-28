using MediatR;

namespace DotNetTwitchBot.Application.ChatMessage.Notification
{
    public class SendBotMessage(string message, PlatformType platform) : INotification
    {
        public SendBotMessage(string message) : this(message, PlatformType.Twitch)
        {
        }

        public string Message { get; } = message;
        public PlatformType Platform { get; } = platform;
    }
}
