namespace PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Stream
{
    public sealed class StreamOffline
    {
        public string BroadcasterUserId { get; set; } = null!;
        public string BroadcasterUserLogin { get; set; } = null!;
        public string BroadcasterUserName { get; set; } = null!;
    }
}