namespace PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Channel;
public sealed class ChannelUnban
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserLogin { get; set; } = string.Empty;
    public string BroadcasterUserId { get; set; } = string.Empty;
    public string BroadcasterUserName { get; set; } = string.Empty;
    public string BroadcasterUserLogin { get; set; } = string.Empty;
    public string ModeratorUserId { get; set; } = string.Empty;
    public string ModeratorUserName { get; set; } = string.Empty;
    public string ModeratorUserLogin { get; set; } = string.Empty;
}