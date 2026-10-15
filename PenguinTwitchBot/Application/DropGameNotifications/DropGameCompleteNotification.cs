namespace PenguinTwitchBot.Application.DropGameNotifications
{
    public class DropGameCompleteNotification(
        string dropToken,
        double? landingX = null,
        int? winningBucketIndex = null) : Application.Notifications.INotification
    {
        public string DropToken { get; } = dropToken;
        public double? LandingX { get; } = landingX;
        public int? WinningBucketIndex { get; } = winningBucketIndex;
    }
}

