using System.Text.Json.Serialization;
using PenguinTwitchBot.TwitchApi.Models.EventSub;
using PenguinTwitchBot.TwitchApi.Models.Moderation;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class ModerationTransport : IModerationTransport
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ModerationTransport(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<CheckAutoModStatusResponse> CheckAutoModStatusAsync(string clientId, string? accessToken, List<AutoModMessage> messages, string broadcasterId)
    {
        using var http = HelixHttp.CreateClient(_httpClientFactory, clientId, accessToken);
        var url = HelixQuery.Build("moderation/enforcements/status", new (string Key, string? Value)[]
        {
            ("broadcaster_id", broadcasterId)
        });
        var request = new CheckAutoModStatusApiRequest(
            Data: messages.Select(m => new AutoModMessageApiItem(m.MsgId, m.MsgText)).ToList());

        using var response = await http.PostAsync(url, HelixJson.CreateJsonContent(request));
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<CheckAutoModStatusApiResponse>(response);
        var results = payload?.Data.Select(x => new AutoModResult(x.MsgId, x.IsPermitted)).ToList() ?? [];
        return new CheckAutoModStatusResponse(results);
    }

    public async Task<GetBannedUsersResponse> GetBannedUsersAsync(string clientId, string? accessToken, string broadcasterId, string? after)
    {
        using var http = HelixHttp.CreateClient(_httpClientFactory, clientId, accessToken);
        var url = BuildBannedUsersUrl(broadcasterId, after);
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<HelixPaginatedDataResponse<BannedUserApiItem>>(response);
        var users = payload?.Data.Select(x => new BannedUser(x.UserId, x.UserLogin, x.ExpiresAt)).ToList() ?? [];
        return new GetBannedUsersResponse(users, payload?.Pagination?.Cursor);
    }

    public async Task<GetModeratorsResponse> GetModeratorsAsync(string clientId, string? accessToken, string broadcasterId, List<string> userIds)
    {
        using var http = HelixHttp.CreateClient(_httpClientFactory, clientId, accessToken);
        var url = BuildModeratorsUrl(broadcasterId, userIds);
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<HelixPaginatedDataResponse<ModeratorApiItem>>(response);
        var moderators = payload?.Data.Select(x => new Moderator(x.UserId, x.UserLogin, x.UserName)).ToList() ?? [];
        return new GetModeratorsResponse(moderators, payload?.Pagination?.Cursor);
    }

    public async Task BanUserAsync(string clientId, string? accessToken, string broadcasterId, string moderatorId, BanUserRequest request)
    {
        using var http = HelixHttp.CreateClient(_httpClientFactory, clientId, accessToken);
        var url = HelixQuery.Build("moderation/bans", new (string Key, string? Value)[]
        {
            ("broadcaster_id", broadcasterId),
            ("moderator_id", moderatorId)
        });
        var body = new BanUserApiRequest(
            Data: new BanUserApiData(
                UserId: request.UserId,
                Reason: request.Reason,
                Duration: request.Duration));

        using var response = await http.PostAsync(url, HelixJson.CreateJsonContent(body, ignoreNullValues: true));
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteChatMessagesAsync(string clientId, string? accessToken, string broadcasterId, string moderatorId, string? messageId)
    {
        using var http = HelixHttp.CreateClient(_httpClientFactory, clientId, accessToken);
        var url = BuildDeleteChatMessagesUrl(broadcasterId, moderatorId, messageId);
        using var response = await http.DeleteAsync(url);
        response.EnsureSuccessStatusCode();
    }

    public async Task<EventSubSubscriptionResult> CreateEventSubSubscriptionAsync(string clientId, string? accessToken, string type, string version, Dictionary<string, string> condition, EventSubTransportMethod transportMethod, string transportSessionId)
    {
        using var http = HelixHttp.CreateClient(_httpClientFactory, clientId, accessToken);
        var url = "eventsub/subscriptions";
        var body = new CreateEventSubSubscriptionApiRequest(
            Type: type,
            Version: version,
            Condition: condition,
            Transport: new EventSubTransportApiRequest(
                Method: transportMethod == EventSubTransportMethod.Websocket ? "websocket" : throw new ArgumentOutOfRangeException(nameof(transportMethod), transportMethod, null),
                SessionId: transportSessionId));

        using var response = await http.PostAsync(url, HelixJson.CreateJsonContent(body));
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<CreateEventSubSubscriptionApiResponse>(response);
        var isEnabled = payload?.Data.Count > 0 && string.Equals(payload.Data[0].Status, "enabled", StringComparison.OrdinalIgnoreCase);
        return new EventSubSubscriptionResult(isEnabled);
    }

    private static string BuildBannedUsersUrl(string broadcasterId, string? after)
    {
        return HelixQuery.Build("moderation/banned", new (string Key, string? Value)[]
        {
            ("broadcaster_id", broadcasterId),
            ("first", "100"),
            ("after", after)
        });
    }

    private static string BuildModeratorsUrl(string broadcasterId, List<string> userIds)
    {
        var parameters = new List<(string Key, string? Value)>
        {
            ("broadcaster_id", broadcasterId)
        };

        parameters.AddRange(HelixQuery.Repeat("user_id", userIds));

        return HelixQuery.Build("moderation/moderators", parameters);
    }

    private static string BuildDeleteChatMessagesUrl(string broadcasterId, string moderatorId, string? messageId)
    {
        return HelixQuery.Build("moderation/chat", new (string Key, string? Value)[]
        {
            ("broadcaster_id", broadcasterId),
            ("moderator_id", moderatorId),
            ("message_id", messageId)
        });
    }

    private sealed record CheckAutoModStatusApiRequest(
        [property: JsonPropertyName("data")] IReadOnlyList<AutoModMessageApiItem> Data);

    private sealed record AutoModMessageApiItem(
        [property: JsonPropertyName("msg_id")] string MsgId,
        [property: JsonPropertyName("msg_text")] string MsgText);

    private sealed record CheckAutoModStatusApiResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<AutoModResultApiItem> Data);

    private sealed record AutoModResultApiItem(
        [property: JsonPropertyName("msg_id")] string MsgId,
        [property: JsonPropertyName("is_permitted")] bool IsPermitted);

    private sealed record BannedUserApiItem(
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("user_login")] string UserLogin,
        [property: JsonPropertyName("expires_at")] DateTime? ExpiresAt);

    private sealed record ModeratorApiItem(
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("user_login")] string UserLogin,
        [property: JsonPropertyName("user_name")] string UserName);

    private sealed record BanUserApiRequest(
        [property: JsonPropertyName("data")] BanUserApiData Data);

    private sealed record BanUserApiData(
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("reason")] string Reason,
        [property: JsonPropertyName("duration")] int? Duration);

    private sealed record CreateEventSubSubscriptionApiRequest(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("condition")] Dictionary<string, string> Condition,
        [property: JsonPropertyName("transport")] EventSubTransportApiRequest Transport);

    private sealed record EventSubTransportApiRequest(
        [property: JsonPropertyName("method")] string Method,
        [property: JsonPropertyName("session_id")] string SessionId);

    private sealed record CreateEventSubSubscriptionApiResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<EventSubSubscriptionApiItem> Data);

    private sealed record EventSubSubscriptionApiItem(
        [property: JsonPropertyName("status")] string Status);
}
