using PenguinTwitchBot.TwitchApi.EventSub.EventArgs.Channel;

namespace PenguinTwitchBot.Application.ChatMessage.Notifications
{
    public class DeletedChatMessage : Application.Notifications.INotification
    {
        public ChannelChatMessageDeleteEventArgs EventArgs { get; set; } = new();
    }
}
