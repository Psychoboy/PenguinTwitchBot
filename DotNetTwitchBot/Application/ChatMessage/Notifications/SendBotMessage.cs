namespace DotNetTwitchBot.Application.ChatMessage.Notification
{
    public class SendBotMessage(string message, bool sourceOnly) : Application.Notifications.INotification
    {
        public string Message { get; } = message;
        public bool SourceOnly { get; } = sourceOnly;
    }
}
