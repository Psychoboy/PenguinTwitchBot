namespace PenguinTwitchBot.Application.DropGameNotifications
{
    public class DropGameCompleteNotification(
        string dropToken,
        double? landingX = null,
        int? winningBucketIndex = null,
        string? overlayKey = null,
        bool? parachuteDestroyed = null) : Application.Notifications.INotification
    {
        public string DropToken { get; } = dropToken;
        public double? LandingX { get; set; } = landingX;
        public int? WinningBucketIndex { get; set; } = winningBucketIndex;
        public string? OverlayKey { get; } = overlayKey;
        public bool? ParachuteDestroyed { get; set; } = parachuteDestroyed;
    }
}

