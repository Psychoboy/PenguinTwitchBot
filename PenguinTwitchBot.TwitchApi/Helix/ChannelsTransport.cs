using System.Text.Json.Serialization;
using PenguinTwitchBot.TwitchApi.Models.Channels;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class ChannelsTransport : IChannelsTransport
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ChannelsTransport(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<GetChannelInformationResponse> GetChannelInformationAsync(string clientId, string? accessToken, string broadcasterId)
    {
        using var http = HelixHttp.CreateClient(_httpClientFactory, clientId, accessToken);
        var url = HelixQuery.Build("channels", new (string Key, string? Value)[]
        {
            ("broadcaster_id", broadcasterId)
        });
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<GetChannelInformationApiResponse>(response);
        var channels = payload?.Data.Select(MapToChannelInformation).ToList() ?? [];
        return new GetChannelInformationResponse(channels);
    }

    public async Task<GetChannelFollowersResponse> GetChannelFollowersAsync(string clientId, string? accessToken, string broadcasterId, string userId, int first, string? after)
    {
        using var http = HelixHttp.CreateClient(_httpClientFactory, clientId, accessToken);
        var url = BuildFollowersUrl(broadcasterId, userId, first, after);
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<GetChannelFollowersApiResponse>(response);
        var followers = payload?.Data.Select(MapToChannelFollower).ToList() ?? [];
        return new GetChannelFollowersResponse(followers);
    }

    public async Task<GetChannelEditorsResponse> GetChannelEditorsAsync(string clientId, string? accessToken, string broadcasterId)
    {
        using var http = HelixHttp.CreateClient(_httpClientFactory, clientId, accessToken);
        var url = HelixQuery.Build("channels/editors", new (string Key, string? Value)[]
        {
            ("broadcaster_id", broadcasterId)
        });
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<GetChannelEditorsApiResponse>(response);
        var editors = payload?.Data.Select(MapToChannelEditor).ToList() ?? [];
        return new GetChannelEditorsResponse(editors);
    }

    private static string BuildFollowersUrl(string broadcasterId, string userId, int first, string? after)
    {
        return HelixQuery.Build("channels/followers", new (string Key, string? Value)[]
        {
            ("broadcaster_id", broadcasterId),
            ("user_id", userId),
            ("first", first.ToString()),
            ("after", after)
        });
    }

    private static ChannelInformation MapToChannelInformation(ChannelInformationApiItem source)
    {
        return new ChannelInformation(
            BroadcasterId: source.BroadcasterId,
            BroadcasterLogin: source.BroadcasterLogin,
            BroadcasterName: source.BroadcasterName,
            BroadcasterLanguage: source.BroadcasterLanguage,
            GameId: source.GameId,
            GameName: source.GameName,
            Title: source.Title,
            Delay: source.Delay);
    }

    private static ChannelFollower MapToChannelFollower(ChannelFollowerApiItem source)
    {
        return new ChannelFollower(
            UserId: source.UserId,
            UserLogin: source.UserLogin,
            UserName: source.UserName,
            FollowedAt: source.FollowedAt);
    }

    private static ChannelEditor MapToChannelEditor(ChannelEditorApiItem source)
    {
        return new ChannelEditor(
            UserId: source.UserId,
            UserName: source.UserName,
            UserLogin: source.UserLogin,
            CreatedAt: source.CreatedAt);
    }

    private sealed record GetChannelInformationApiResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<ChannelInformationApiItem> Data);

    private sealed record ChannelInformationApiItem(
        [property: JsonPropertyName("broadcaster_id")] string BroadcasterId,
        [property: JsonPropertyName("broadcaster_login")] string BroadcasterLogin,
        [property: JsonPropertyName("broadcaster_name")] string BroadcasterName,
        [property: JsonPropertyName("game_id")] string GameId,
        [property: JsonPropertyName("broadcaster_language")] string BroadcasterLanguage,
        [property: JsonPropertyName("game_name")] string GameName,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("delay")] int Delay);

    private sealed record GetChannelFollowersApiResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<ChannelFollowerApiItem> Data);

    private sealed record ChannelFollowerApiItem(
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("user_login")] string UserLogin,
        [property: JsonPropertyName("user_name")] string UserName,
        [property: JsonPropertyName("followed_at")] string FollowedAt);

    private sealed record GetChannelEditorsApiResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<ChannelEditorApiItem> Data);

    private sealed record ChannelEditorApiItem(
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("user_name")] string UserName,
        [property: JsonPropertyName("user_login")] string UserLogin,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt);
}
