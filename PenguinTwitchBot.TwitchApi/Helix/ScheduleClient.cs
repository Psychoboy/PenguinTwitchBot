using PenguinTwitchBot.Bot.Twitch.Models.Schedule;
using TwitchLib.Api.Helix.Models.Schedule.GetChannelStreamSchedule;
using TwitchLibChannelStreamSchedule = TwitchLib.Api.Helix.Models.Schedule.ChannelStreamSchedule;

namespace PenguinTwitchBot.Bot.Twitch.Helix;

public sealed class ScheduleClient(ILogger<ScheduleClient> logger, IScheduleTransport transport) : TwitchClientRetryBase(logger), IScheduleClient
{
    public Task<GetChannelStreamScheduleResponse> GetChannelStreamScheduleAsync(string clientId, string? accessToken, string broadcasterId)
    {
        return ExecuteWithRetryAsync(() => transport.GetChannelStreamScheduleAsync(clientId, accessToken, broadcasterId), "get channel stream schedule");
    }

    public static ChannelStreamSchedule MapToChannelStreamSchedule(TwitchLibChannelStreamSchedule source)
    {
        var vacation = source.Vacation != null
            ? new ChannelStreamScheduleVacation(source.Vacation.StartTime, source.Vacation.EndTime)
            : null;

        var segments = source.Segments?.Select(s => new StreamScheduleSegment(
            Id: s.Id,
            StartTime: s.StartTime,
            EndTime: s.EndTime,
            Title: s.Title,
            CanceledUntil: s.CanceledUntil,
            IsRecurring: s.IsRecurring
        )).ToList() ?? [];

        return new ChannelStreamSchedule(
            BroadcasterId: source.BroadcasterId,
            Segments: segments,
            Vacation: vacation);
    }
}
