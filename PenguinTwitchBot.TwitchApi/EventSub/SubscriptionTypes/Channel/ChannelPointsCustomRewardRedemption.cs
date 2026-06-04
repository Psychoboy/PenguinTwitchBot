using PenguinTwitchBot.TwitchApi.EventSub.Models.ChannelPoints;

namespace PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Channel;

public sealed class ChannelPointsCustomRewardRedemption
{
    public string Id { get; set; } = string.Empty;
    public string BroadcasterUserId { get; set; } = string.Empty;
    public string BroadcasterUserLogin { get; set; } = string.Empty;
    public string BroadcasterUserName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserLogin { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserInput { get; set; } = string.Empty;

    //
    // Summary:
    //     Status of the redemption. Possible values are unknown, unfulfilled, fulfilled,
    //     and canceled.
    public string Status { get; set; } = string.Empty;
    public RedemptionReward Reward { get; set; } = new RedemptionReward();
    public DateTimeOffset RedeemedAt { get; set; } = DateTimeOffset.MinValue;
}