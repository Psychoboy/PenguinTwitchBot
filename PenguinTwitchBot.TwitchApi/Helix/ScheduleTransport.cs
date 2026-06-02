using System.Text.Json.Serialization;
using PenguinTwitchBot.TwitchApi.Models.Schedule;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class ScheduleTransport : IScheduleTransport
{
    public async Task<GetChannelStreamScheduleResponse> GetChannelStreamScheduleAsync(string clientId, string? accessToken, string broadcasterId)
    {
        using var http = HelixHttp.CreateClient(clientId, accessToken);
        var url = HelixHttp.BuildUrl($"schedule?broadcaster_id={Uri.EscapeDataString(broadcasterId)}");
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<GetChannelStreamScheduleApiResponse>(response);
        return new GetChannelStreamScheduleResponse(
            Schedule: payload?.Schedule != null ? MapToSchedule(payload.Schedule) : null,
            Cursor: payload?.Pagination?.Cursor);
    }

    private static ChannelStreamSchedule MapToSchedule(ChannelStreamScheduleApiItem source)
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

    private sealed record GetChannelStreamScheduleApiResponse(
        [property: JsonPropertyName("data")] ChannelStreamScheduleApiItem? Schedule,
        [property: JsonPropertyName("pagination")] PaginationApiItem? Pagination);

    private sealed record PaginationApiItem(
        [property: JsonPropertyName("cursor")] string? Cursor);

    private sealed record ChannelStreamScheduleApiItem(
        [property: JsonPropertyName("segments")] IReadOnlyList<SegmentApiItem>? Segments,
        [property: JsonPropertyName("broadcaster_id")] string BroadcasterId,
        [property: JsonPropertyName("vacation")] VacationApiItem? Vacation);

    private sealed record SegmentApiItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("start_time")] DateTime StartTime,
        [property: JsonPropertyName("end_time")] DateTime? EndTime,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("canceled_until")] DateTime? CanceledUntil,
        [property: JsonPropertyName("is_recurring")] bool IsRecurring);

    private sealed record VacationApiItem(
        [property: JsonPropertyName("start_time")] DateTime StartTime,
        [property: JsonPropertyName("end_time")] DateTime EndTime);
}
