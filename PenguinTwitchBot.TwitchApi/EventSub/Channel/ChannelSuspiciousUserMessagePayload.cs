namespace PenguinTwitchBot.TwitchApi.EventSub.Channel;

public sealed class ChannelSuspiciousUserMessagePayload : EventSubPayload<ChannelSuspiciousUserMessage>
{
}

public sealed class ChannelSuspiciousUserMessage
{
    public required string UserId { get; set; }
    public required string UserLogin { get; set; }
    public required string UserName { get; set; }
    public required ChannelSuspiciousMessage Message { get; set; }
}

public sealed class ChannelSuspiciousMessage
{
    public required string MessageId { get; set; }
    public required string Text { get; set; }
}
