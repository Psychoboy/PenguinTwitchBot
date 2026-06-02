namespace PenguinTwitchBot.TwitchApi.EventSub.Channel;

public sealed class ChannelBanPayload : EventSubPayload<ChannelBan>
{
}

public sealed class ChannelBan
{
    public required string UserId { get; set; }
    public required string UserLogin { get; set; }
    public required string UserName { get; set; }
    public required string ModeratorUserId { get; set; }
    public required string ModeratorUserLogin { get; set; }
    public required string ModeratorUserName { get; set; }
    public bool IsPermanent { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public DateTimeOffset BannedAt { get; set; }
}
