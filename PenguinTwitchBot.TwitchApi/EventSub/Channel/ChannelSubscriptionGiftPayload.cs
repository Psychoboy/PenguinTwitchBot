namespace PenguinTwitchBot.TwitchApi.EventSub.Channel;

public sealed class ChannelSubscriptionGiftPayload : EventSubPayload<ChannelSubscriptionGift>
{
}

public sealed class ChannelSubscriptionGift
{
    public required string UserId { get; set; }
    public required string UserLogin { get; set; }
    public required string UserName { get; set; }
    public int Total { get; set; }
    public int CumulativeTotal { get; set; }
    public required string Tier { get; set; }
    public bool IsAnonymous { get; set; }
}

