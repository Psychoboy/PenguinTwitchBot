using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Application.ChatMessage.Notifications
{
    public class ReceivedChatMessage : Application.Notifications.INotification
    {
        public ChatMessageEventArgs EventArgs { get; set; } = new();
    }
}
