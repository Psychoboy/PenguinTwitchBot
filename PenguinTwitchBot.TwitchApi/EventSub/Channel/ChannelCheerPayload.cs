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
    public string? UserId { get; set; }
    public string? UserLogin { get; set; }
    public string? UserName { get; set; }
}
