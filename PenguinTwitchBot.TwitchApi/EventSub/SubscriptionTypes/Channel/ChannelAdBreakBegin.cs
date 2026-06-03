namespace PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Channel
{
    public sealed class ChannelAdBreakBegin
    {
        public int DurationSeconds { get; set; }

        public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.MinValue;

        public bool IsAutomatic { get; set; }

        public string BroadcasterUserId { get; set; } = string.Empty;

        public string BroadcasterUserLogin { get; set; } = string.Empty;

        public string BroadcasterUserName { get; set; } = string.Empty;

        public string RequesterUserId { get; set; } = string.Empty;

        public string RequesterUserLogin { get; set; } = string.Empty;

        public string RequesterUserName { get; set; } = string.Empty;
    }
}