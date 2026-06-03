using System.Diagnostics.CodeAnalysis;

namespace PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Channel;
public sealed class ChannelBan
{
    public string UserId { get; init; } = string.Empty;
    public string UserLogin { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string BroadcasterUserId { get; init; } = string.Empty;
    public string BroadcasterUserLogin { get; init; } = string.Empty;
    public string BroadcasterUserName { get; init; } = string.Empty;
    public string ModeratorUserId { get; set; } = string.Empty;
    public string ModeratorUserLogin { get; set; } = string.Empty;
    public string ModeratorUserName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset BannedAt { get; set; } = DateTimeOffset.MinValue;
    public DateTimeOffset? EndsAt { get; set; }
    [MemberNotNullWhen(false, nameof(EndsAt))]
    public bool IsPermanent { get; set; }
}