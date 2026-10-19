namespace PenguinTwitchBot.Application.DropGameNotifications
{
    public class DropGameHeartbeatNotification(
        string game,
        IReadOnlyList<string> activeTokens,
        IReadOnlyList<string> completingTokens,
        string? overlayKey) : Application.Notifications.INotification
    {
        public string Game { get; } = game;
        public IReadOnlyList<string> ActiveTokens { get; } = activeTokens;
        public IReadOnlyList<string> CompletingTokens { get; } = completingTokens;
        public string? OverlayKey { get; } = overlayKey;
    }
}

