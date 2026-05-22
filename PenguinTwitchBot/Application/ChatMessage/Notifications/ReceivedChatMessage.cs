using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Application.ChatMessage.Notifications
{
    public class ReceivedChatMessage : Application.Notifications.INotification
    {
        public ChatMessageEventArgs EventArgs { get; set; } = new();
    }
}
