namespace PenguinTwitchBot.TwitchApi.EventSub.Channel;

public sealed class ChannelPointRedemptionPayload : EventSubPayload<ChannelPointRedemption>
{
}

public sealed class ChannelPointRedemption
{
    public required string Id { get; set; }
    public required string UserId { get; set; }
    public required string UserLogin { get; set; }
    public required string UserName { get; set; }
    public required string Status { get; set; }
    public required string UserInput { get; set; }
    public required ChannelPointReward Reward { get; set; }
    public DateTimeOffset RedeemedAt { get; set; }
}

public sealed class ChannelPointReward
{
    public required string Id { get; set; }
    public required string Title { get; set; }
}
