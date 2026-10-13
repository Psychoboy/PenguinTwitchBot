namespace PenguinTwitchBot.Application.DropGameNotifications
{
    public class DropGameCompleteNotification(string dropToken) : Application.Notifications.INotification
    {
        public string DropToken { get; } = dropToken;
    }
}

