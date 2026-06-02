namespace PenguinTwitchBot.TwitchApi.Models.Schedule;

/// <summary>
/// Domain response model for channel stream schedule.
/// </summary>
public sealed record GetChannelStreamScheduleResponse(
    ChannelStreamSchedule? Schedule,
    string? Cursor);