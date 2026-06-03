using System.Diagnostics.CodeAnalysis;

namespace PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Channel;

public sealed class ChannelCheer
{
    [MemberNotNullWhen(false, ["UserId", "UserLogin", "UserName"])]
    public bool IsAnonymous { get; set; }
    public int Bits { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserLogin { get; set; }
    public string? UserName { get; set; }
    public string BroadcasterUserId { get; set; } = string.Empty;
    public string BroadcasterUserName { get; set; } = string.Empty;
    public string BroadcasterUserLogin { get; set; } = string.Empty;
}