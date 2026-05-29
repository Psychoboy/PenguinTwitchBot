namespace PenguinTwitchBot.Application.ChatMessage.Notifications
{
    public class BannedChatUser : Application.Notifications.INotification
    {
        public string UserId { get; set; } = "";
    }
}
