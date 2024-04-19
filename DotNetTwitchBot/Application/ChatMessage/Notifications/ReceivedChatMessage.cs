using DotNetTwitchBot.Bot.Events.Chat;
using MediatR;

namespace DotNetTwitchBot.Application.ChatMessage.Notifications
{
    public class ReceivedChatMessage : INotification
    {
        public ChatMessageEventArgs EventArgs { get; set; } = new();
    }
}
