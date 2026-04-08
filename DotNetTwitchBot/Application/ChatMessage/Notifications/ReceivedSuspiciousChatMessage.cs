using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Application.ChatMessage.Notifications
{
    public class ReceivedSuspiciousChatMessage : Application.Notifications.INotification
    {
        public ChatMessageEventArgs EventArgs { get; set; } = new();
    }
}
