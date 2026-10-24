namespace PenguinTwitchBot.Application.DropGameNotifications
{
    public class DropGameCompleteNotification(
        string dropToken,
        double? landingX = null,
        int? winningBucketIndex = null,
        string? overlayKey = null,
        bool? parachuteDestroyed = null,
        int? score = null,
        bool? isBullseye = null,
        bool? isWin = null,
        bool? isHit = null,
        bool? balloonPopped = null,
        int? strokeNumber = null,
        int? baseScore = null,
        int? strokePenalty = null,
        double? startAxisPosition = null,
        bool? isLipOut = null,
        double? greenRadiusRatio = null) : Application.Notifications.INotification
    {
        public string DropToken { get; } = dropToken;
        public double? LandingX { get; set; } = landingX;
        public int? WinningBucketIndex { get; set; } = winningBucketIndex;
        public string? OverlayKey { get; } = overlayKey;
        public bool? ParachuteDestroyed { get; set; } = parachuteDestroyed;
        public int? Score { get; set; } = score;
        public bool? IsBullseye { get; set; } = isBullseye;
        public bool? IsWin { get; set; } = isWin;
        public bool? IsHit { get; set; } = isHit;
        public bool? BalloonPopped { get; set; } = balloonPopped;
        public int? StrokeNumber { get; set; } = strokeNumber;
        public int? BaseScore { get; set; } = baseScore;
        public int? StrokePenalty { get; set; } = strokePenalty;
        public double? StartAxisPosition { get; set; } = startAxisPosition;
        public bool? IsLipOut { get; set; } = isLipOut;
        public double? GreenRadiusRatio { get; set; } = greenRadiusRatio;
    }
}

