namespace PenguinTwitchBot.Application.ChatMessage.Notifications
{
    public class DeletedChatMessage : Application.Notifications.INotification
    {
        public ChannelChatMessageDeleteArgs EventArgs { get; set; } = new();
    }
}
