namespace PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Channel
{
    public sealed class ChannelChatMessageDelete
    {
        public string BroadcasterUserId { get; set; } = string.Empty;

        public string BroadcasterUserName { get; set; } = string.Empty;

        public string BroadcasterUserLogin { get; set; } = string.Empty;

        public string TargetUserId { get; set; } = string.Empty;

        public string TargetUserName { get; set; } = string.Empty;

        public string TargetUserLogin { get; set; } = string.Empty;

        public string MessageId { get; set; } = string.Empty;
    }
}