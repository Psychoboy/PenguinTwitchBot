namespace PenguinTwitchBot.Bot.Twitch.Models;

/// <summary>
/// Domain model for Twitch channel stream schedule
/// </summary>
public record ChannelStreamSchedule(
    string BroadcasterId,
    List<StreamScheduleSegment> Segments,
    ChannelStreamScheduleVacation? Vacation = null);

/// <summary>
/// A single stream schedule segment
/// </summary>
public record StreamScheduleSegment(
    string Id,
    DateTime StartTime,
    DateTime? EndTime,
    string Title,
    DateTime? CanceledUntil = null,
    bool IsRecurring = false);

/// <summary>
/// Vacation period for a channel's stream schedule
/// </summary>
public record ChannelStreamScheduleVacation(
    DateTime StartTime,
    DateTime EndTime);
