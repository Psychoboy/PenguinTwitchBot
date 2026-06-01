namespace PenguinTwitchBot.TwitchApi.EventSub.Channel;

public sealed class ChannelUnbanPayload : EventSubPayload<ChannelUnban>
{
}

public sealed class ChannelUnban
{
    public required string UserId { get; set; }
    public required string UserLogin { get; set; }
    public required string UserName { get; set; }
    public required string ModeratorUserId { get; set; }
    public required string ModeratorUserLogin { get; set; }
    public required string ModeratorUserName { get; set; }
}
