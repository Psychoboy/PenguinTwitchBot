namespace PenguinTwitchBot.TwitchApi.EventSub.Models.ChannelSuspiciousUser
{
    public abstract class ChannelSuspiciousUserBase
    {
        public string BroadcasterUserId { get; init; } = string.Empty;
        public string BroadcasterUserName { get; init; } = string.Empty;
        public string BroadcasterUserLogin { get; init; } = string.Empty;
        public string UserId { get; init; } = string.Empty;
        public string UserName { get; init; } = string.Empty;
        public string UserLogin { get; init; } = string.Empty;
        public string LowTrustStatus { get; init; } = string.Empty;
    }
}