namespace PenguinTwitchBot.TwitchApi.EventSub.Channel;

public sealed class ChannelFollowPayload : EventSubPayload<ChannelFollow>
{
}

public sealed class ChannelFollow
{
    public required string UserId { get; set; }
    public required string UserLogin { get; set; }
    public required string UserName { get; set; }
    public DateTime FollowedAt { get; set; }
}
