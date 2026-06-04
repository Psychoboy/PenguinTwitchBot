using PenguinTwitchBot.TwitchApi.EventSub.EventArgs.Channel;

namespace PenguinTwitchBot.Application.ChatMessage.Notifications
{
    public class DeletedChatMessage : Application.Notifications.INotification
    {
        public required ChannelChatMessageDeleteEventArgs EventArgs { get; set; }
    }
}
