using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Application.Notifications;

namespace DotNetTwitchBot.Application.ChatMessage.Notifications
{
    public class ReceivedSuspiciousChatMessage : INotification
    {
        public ChatMessageEventArgs EventArgs { get; set; } = new();
    }
}
