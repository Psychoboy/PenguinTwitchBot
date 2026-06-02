namespace PenguinTwitchBot.TwitchApi.EventSub.Channel;

public sealed class ChannelSubscribePayload : EventSubPayload<ChannelSubscribe>
{
}

public sealed class ChannelSubscribe
{
    public required string UserId { get; set; }
    public required string UserLogin { get; set; }
    public required string UserName { get; set; }
    public required string Tier { get; set; }
    public bool IsGift { get; set; }
}
