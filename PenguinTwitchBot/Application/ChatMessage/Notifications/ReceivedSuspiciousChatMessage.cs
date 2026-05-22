using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Application.ChatMessage.Notifications
{
    public class ReceivedSuspiciousChatMessage : Application.Notifications.INotification
    {
        public ChatMessageEventArgs EventArgs { get; set; } = new();
    }
}
