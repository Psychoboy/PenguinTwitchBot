namespace PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Channel;

public sealed class ChannelRaid
{
    public string FromBroadcasterUserId { get; set; } = string.Empty;
    public string FromBroadcasterUserName { get; set; } = string.Empty;
    public string FromBroadcasterUserLogin { get; set; } = string.Empty;
    public string ToBroadcasterUserId { get; set; } = string.Empty;
    public string ToBroadcasterUserName { get; set; } = string.Empty;
    public string ToBroadcasterUserLogin { get; set; } = string.Empty;
    public int Viewers { get; set; }
}