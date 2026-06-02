using System.Text.Json.Serialization;
using PenguinTwitchBot.TwitchApi.Models.Schedule;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class ScheduleTransport : IScheduleTransport
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ScheduleTransport(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<GetChannelStreamScheduleResponse> GetChannelStreamScheduleAsync(string clientId, string? accessToken, string broadcasterId)
    {
        using var http = HelixHttp.CreateClient(_httpClientFactory, clientId, accessToken);
        var url = HelixQuery.Build("schedule", new (string Key, string? Value)[]
        {
            ("broadcaster_id", broadcasterId)
        });
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<HelixObjectWithPaginationResponse<ChannelStreamScheduleApiItem>>(response);
        return new GetChannelStreamScheduleResponse(
            Schedule: payload?.Data != null ? MapToSchedule(payload.Data) : null,
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
