using System.Text.Json.Serialization;
using PenguinTwitchBot.TwitchApi.Models.Streams;
using StreamModel = PenguinTwitchBot.TwitchApi.Models.Streams.Stream;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class StreamsTransport : IStreamsTransport
{
    private readonly IHttpClientFactory _httpClientFactory;

    public StreamsTransport(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<GetStreamsResponse> GetStreamsAsync(string clientId, string? accessToken, List<string>? userIds)
    {
        using var http = HelixHttp.CreateClient(_httpClientFactory, clientId, accessToken);
        var url = BuildStreamsUrl(userIds);
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<GetStreamsApiResponse>(response);
        var streams = payload?.Data.Select(MapToStream).ToList() ?? [];
        return new GetStreamsResponse(streams);
    }

    private static string BuildStreamsUrl(List<string>? userIds)
    {
        var parameters = new List<(string Key, string? Value)>
        {
            ("first", "100")
        };
        parameters.AddRange(HelixQuery.Repeat("user_id", userIds));

        return HelixQuery.Build("streams", parameters);
    }

    private static StreamModel MapToStream(StreamApiItem source)
    {
        return new StreamModel(
            Id: source.Id,
            UserId: source.UserId,
            UserLogin: source.UserLogin,
            UserName: source.UserName,
            GameId: source.GameId,
            GameName: source.GameName,
            Type: source.Type,
            Title: source.Title,
            Tags: source.Tags ?? [],
            ViewerCount: source.ViewerCount,
            StartedAt: source.StartedAt,
            Language: source.Language,
            ThumbnailUrl: source.ThumbnailUrl,
            IsMature: source.IsMature);
    }

    private sealed record GetStreamsApiResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<StreamApiItem> Data);

    private sealed record StreamApiItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("user_login")] string UserLogin,
        [property: JsonPropertyName("user_name")] string UserName,
        [property: JsonPropertyName("game_id")] string GameId,
        [property: JsonPropertyName("game_name")] string GameName,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("tags")] IReadOnlyList<string>? Tags,
        [property: JsonPropertyName("viewer_count")] int ViewerCount,
        [property: JsonPropertyName("started_at")] DateTime StartedAt,
        [property: JsonPropertyName("language")] string Language,
        [property: JsonPropertyName("thumbnail_url")] string ThumbnailUrl,
        [property: JsonPropertyName("is_mature")] bool IsMature);
}
