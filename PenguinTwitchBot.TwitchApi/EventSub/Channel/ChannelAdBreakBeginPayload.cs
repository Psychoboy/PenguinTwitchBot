namespace PenguinTwitchBot.TwitchApi.EventSub.Channel;

public sealed class ChannelAdBreakBeginPayload : EventSubPayload<ChannelAdBreakBegin>
{
}

public sealed class ChannelAdBreakBegin
{
    public int DurationSeconds { get; set; }
    public bool IsAutomatic { get; set; }
    public DateTimeOffset StartedAt { get; set; }
}
