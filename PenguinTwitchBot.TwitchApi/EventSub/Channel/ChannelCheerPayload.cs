namespace PenguinTwitchBot.TwitchApi.EventSub.Channel;

public sealed class ChannelCheerPayload : EventSubPayload<ChannelCheer>
{
}

public sealed class ChannelCheer
{
    public bool IsAnonymous { get; set; }
    public string? CheererId { get; set; }
    public string? CheererLogin { get; set; }
    public string? CheererName { get; set; }
    public int Bits { get; set; }
    public string? Message { get; set; }
    public required string UserId { get; set; }
    public required string UserLogin { get; set; }
    public required string UserName { get; set; }
}
