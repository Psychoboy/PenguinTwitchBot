namespace PenguinTwitchBot.TwitchApi.EventSub.Channel;

public sealed class ChannelChatMessagePayload : EventSubPayload<ChannelChatMessage>
{
}

public sealed class ChannelChatMessage
{
    public required string MessageId { get; set; }
    public required string ChatterUserId { get; set; }
    public required string ChatterUserLogin { get; set; }
    public required string ChatterUserName { get; set; }
    public bool IsSubscriber { get; set; }
    public bool IsModerator { get; set; }
    public bool IsVip { get; set; }
    public bool IsBroadcaster { get; set; }
    public required string Message { get; set; }
    public required string ChannelPointsCustomRewardId { get; set; }
    public string? SourceBroadcasterUserId { get; set; }
}
