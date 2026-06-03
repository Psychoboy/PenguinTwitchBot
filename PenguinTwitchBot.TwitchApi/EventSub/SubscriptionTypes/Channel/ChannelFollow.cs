namespace PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Channel;

public sealed class ChannelFollow
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserLogin { get; set; } = string.Empty;
    public string BroadcasterUserId { get; set; } = string.Empty;
    public string BroadcasterUserName { get; set; } = string.Empty;
    public string BroadcasterUserLogin { get; set; } = string.Empty;
    public DateTimeOffset FollowedAt { get; set; } = DateTimeOffset.MinValue;
}