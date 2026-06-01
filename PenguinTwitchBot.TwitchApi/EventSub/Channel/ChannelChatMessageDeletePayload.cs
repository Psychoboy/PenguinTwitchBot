namespace PenguinTwitchBot.TwitchApi.EventSub.Channel;

public sealed class ChannelChatMessageDeletePayload : EventSubPayload<ChannelChatMessageDelete>
{
}

public sealed class ChannelChatMessageDelete
{
    public required string MessageId { get; set; }
    public required string TargetUserName { get; set; }
    public required string TargetUserLogin { get; set; }
    public required string TargetUserId { get; set; }
}
