namespace PenguinTwitchBot.TwitchApi.EventSub.Channel;

public sealed class ChannelChatNotificationPayload : EventSubPayload<ChannelChatNotification>
{
}

public sealed class ChannelChatNotification
{
    public required string MessageId { get; set; }
    public required string ChatterUserId { get; set; }
    public required string ChatterUserLogin { get; set; }
    public required string ChatterUserName { get; set; }
    public required string NoticeType { get; set; }
    public required string Message { get; set; }
}
