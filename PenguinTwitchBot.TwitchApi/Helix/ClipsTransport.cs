using System.Text.Json.Serialization;
using PenguinTwitchBot.TwitchApi.Models.Clips;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class ClipsTransport : IClipsTransport
{
    public async Task<GetClipsResponse> GetClipsAsync(string clientId, string? accessToken, string? broadcasterId, string? userId, int first, bool? isFeatured)
    {
        using var http = HelixHttp.CreateClient(clientId, accessToken);
        var url = HelixHttp.BuildUrl(BuildClipsUrl(broadcasterId, userId, first, isFeatured));
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<GetClipsApiResponse>(response);
        var clips = payload?.Data.Select(MapToClip).ToList() ?? [];
        return new GetClipsResponse(clips);
    }

    public async Task<GetClipsResponse> GetClipsByIdAsync(string clientId, string? accessToken, List<string> clipIds)
    {
        using var http = HelixHttp.CreateClient(clientId, accessToken);
        var url = HelixHttp.BuildUrl(BuildClipIdsUrl(clipIds));
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<GetClipsApiResponse>(response);
        var clips = payload?.Data.Select(MapToClip).ToList() ?? [];
        return new GetClipsResponse(clips);
    }

    private static string BuildClipsUrl(string? broadcasterId, string? userId, int first, bool? isFeatured)
    {
        var clipBroadcasterId = !string.IsNullOrWhiteSpace(broadcasterId)
            ? broadcasterId
            : userId;

        var queryParts = new List<string> { $"first={first}" };

        if (!string.IsNullOrWhiteSpace(clipBroadcasterId))
        {
            queryParts.Add($"broadcaster_id={Uri.EscapeDataString(clipBroadcasterId)}");
        }

        if (isFeatured.HasValue)
        {
            queryParts.Add($"is_featured={isFeatured.Value.ToString().ToLowerInvariant()}");
        }

        return $"clips?{string.Join("&", queryParts)}";
    }

    private static string BuildClipIdsUrl(List<string> clipIds)
    {
        var queryParts = new List<string>();
        foreach (var clipId in clipIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            queryParts.Add($"id={Uri.EscapeDataString(clipId)}");
        }

        return $"clips?{string.Join("&", queryParts)}&first=100";
    }

    private static Clip MapToClip(ClipApiItem source)
    {
        return new Clip(
            Id: source.Id,
            Url: source.Url,
            EmbedUrl: source.EmbedUrl,
            Title: source.Title,
            ViewCount: source.ViewCount,
            CreatedAt: DateTime.Parse(source.CreatedAt),
            Language: source.Language,
            ThumbnailUrl: source.ThumbnailUrl,
            BroadcasterName: source.BroadcasterName,
            BroadcasterId: source.BroadcasterId,
            CreatorName: source.CreatorName,
            CreatorId: source.CreatorId,
            Duration: source.Duration,
            VideoId: source.VideoId,
            GameId: source.GameId);
    }

    private sealed record GetClipsApiResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<ClipApiItem> Data);

    private sealed record ClipApiItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("embed_url")] string EmbedUrl,
        [property: JsonPropertyName("broadcaster_id")] string BroadcasterId,
        [property: JsonPropertyName("broadcaster_name")] string BroadcasterName,
        [property: JsonPropertyName("creator_id")] string CreatorId,
        [property: JsonPropertyName("creator_name")] string CreatorName,
        [property: JsonPropertyName("video_id")] string? VideoId,
        [property: JsonPropertyName("game_id")] string? GameId,
        [property: JsonPropertyName("language")] string Language,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("view_count")] int ViewCount,
        [property: JsonPropertyName("created_at")] string CreatedAt,
        [property: JsonPropertyName("thumbnail_url")] string ThumbnailUrl,
        [property: JsonPropertyName("duration")] float Duration);
}
