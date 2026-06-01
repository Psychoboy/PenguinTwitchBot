namespace PenguinTwitchBot.TwitchApi.EventSub.Channel;

public sealed class ChannelSubscriptionRenewalPayload : EventSubPayload<ChannelSubscriptionRenewal>
{
}

public sealed class ChannelSubscriptionRenewal
{
    public required string UserId { get; set; }
    public required string UserLogin { get; set; }
    public required string UserName { get; set; }
    public int CumulativeMonths { get; set; }
    public int StreakMonths { get; set; }
    public required string Tier { get; set; }
    public required SubscriptionMessage Message { get; set; }
}

public sealed class SubscriptionMessage
{
    public string? Text { get; set; }
}
