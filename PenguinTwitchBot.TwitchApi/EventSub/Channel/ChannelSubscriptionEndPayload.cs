namespace PenguinTwitchBot.TwitchApi.EventSub.Channel;

public sealed class ChannelSubscriptionEndPayload : EventSubPayload<ChannelSubscriptionEnd>
{
}

public sealed class ChannelSubscriptionEnd
{
    public required string UserId { get; set; }
    public required string UserLogin { get; set; }
    public required string UserName { get; set; }
}
