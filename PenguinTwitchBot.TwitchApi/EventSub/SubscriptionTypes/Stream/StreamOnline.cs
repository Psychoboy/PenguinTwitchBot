namespace PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Stream
{
    public sealed class StreamOnline
    {
        public string Id { get; set; } = string.Empty;
        public string BroadcasterUserId { get; set; } = string.Empty;
        public string BroadcasterUserLogin { get; set; } = string.Empty;
        public string BroadcasterUserName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.MinValue;
    }
}