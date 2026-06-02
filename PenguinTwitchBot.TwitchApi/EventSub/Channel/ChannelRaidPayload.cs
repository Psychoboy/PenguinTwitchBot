namespace PenguinTwitchBot.TwitchApi.EventSub.Channel;

public sealed class ChannelRaidPayload : EventSubPayload<ChannelRaid>
{
}

public sealed class ChannelRaid
{
    public required string FromBroadcasterUserId { get; set; }
    public required string FromBroadcasterUserLogin { get; set; }
    public required string FromBroadcasterUserName { get; set; }
    public required string ToBroadcasterUserId { get; set; }
    public required string ToBroadcasterUserLogin { get; set; }
    public required string ToBroadcasterUserName { get; set; }
    public int Viewers { get; set; }
}
