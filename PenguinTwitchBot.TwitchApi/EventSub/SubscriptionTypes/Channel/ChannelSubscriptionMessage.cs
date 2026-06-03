using PenguinTwitchBot.TwitchApi.EventSub.Models.Subscriptions;
namespace PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Channel;

public sealed class ChannelSubscriptionMessage
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserLogin { get; set; } = string.Empty;
    public string BroadcasterUserId { get; set; } = string.Empty;
    public string BroadcasterUserName { get; set; } = string.Empty;
    public string BroadcasterUserLogin { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public SubscriptionMessage Message { get; set; } = new();
    public int CumulativeMonths { get; set; }
    public int? StreakMonths { get; set; }
    public int DurationMonths { get; set; }
}