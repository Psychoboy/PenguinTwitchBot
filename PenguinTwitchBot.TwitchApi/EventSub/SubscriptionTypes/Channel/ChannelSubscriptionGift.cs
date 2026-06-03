using System.Diagnostics.CodeAnalysis;

namespace PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Channel;

public sealed class ChannelSubscriptionGift
{
    public string? UserId { get; set; } = string.Empty;
    public string? UserName { get; set; } = string.Empty;
    public string? UserLogin { get; set; } = string.Empty;
    public string BroadcasterUserId { get; set; } = string.Empty;
    public string BroadcasterUserName { get; set; } = string.Empty;
    public string BroadcasterUserLogin { get; set; } = string.Empty;
    public int Total { get; set; }
    public string Tier { get; set; } = string.Empty;
    public int? CumulativeTotal { get; set; }
    [MemberNotNullWhen(false, ["UserId", "UserLogin", "UserName"])]
    public bool IsAnonymous { get; set; }
}