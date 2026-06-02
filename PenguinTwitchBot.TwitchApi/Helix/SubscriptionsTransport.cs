using System.Text.Json.Serialization;
using PenguinTwitchBot.TwitchApi.Models.Subscriptions;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class SubscriptionsTransport : ISubscriptionsTransport
{
    public async Task<CheckUserSubscriptionResponse> CheckUserSubscriptionAsync(string clientId, string? accessToken, string broadcasterId, string userId)
    {
        using var http = HelixHttp.CreateClient(clientId, accessToken);
        var url = HelixHttp.BuildUrl($"subscriptions/user?broadcaster_id={Uri.EscapeDataString(broadcasterId)}&user_id={Uri.EscapeDataString(userId)}");
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<CheckUserSubscriptionApiResponse>(response);
        var subscriptions = payload?.Data.Select(MapToSubscription).ToList() ?? [];
        return new CheckUserSubscriptionResponse(subscriptions);
    }

    public async Task<GetBroadcasterSubscriptionsResponse> GetBroadcasterSubscriptionsAsync(string clientId, string? accessToken, string broadcasterId, int first, string? after)
    {
        using var http = HelixHttp.CreateClient(clientId, accessToken);
        var url = HelixHttp.BuildUrl(BuildBroadcasterSubscriptionsUrl(broadcasterId, first, after));
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<GetBroadcasterSubscriptionsApiResponse>(response);
        var subscriptions = payload?.Data.Select(MapToSubscription).ToList() ?? [];
        return new GetBroadcasterSubscriptionsResponse(
            Data: subscriptions,
            Cursor: payload?.Pagination?.Cursor,
            Total: payload?.Total ?? 0,
            Points: payload?.Points ?? 0);
    }

    private static string BuildBroadcasterSubscriptionsUrl(string broadcasterId, int first, string? after)
    {
        var queryParts = new List<string>
        {
            $"broadcaster_id={Uri.EscapeDataString(broadcasterId)}",
            $"first={first}"
        };

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParts.Add($"after={Uri.EscapeDataString(after)}");
        }

        return $"subscriptions?{string.Join("&", queryParts)}";
    }

    private static Subscription MapToSubscription(SubscriptionApiItem source)
    {
        return new Subscription(
            UserId: source.UserId,
            UserLogin: source.UserLogin,
            UserName: source.UserName);
    }

    private sealed record CheckUserSubscriptionApiResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<SubscriptionApiItem> Data);

    private sealed record GetBroadcasterSubscriptionsApiResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<SubscriptionApiItem> Data,
        [property: JsonPropertyName("pagination")] PaginationApiItem? Pagination,
        [property: JsonPropertyName("total")] int Total,
        [property: JsonPropertyName("points")] int Points);

    private sealed record PaginationApiItem(
        [property: JsonPropertyName("cursor")] string? Cursor);

    private sealed record SubscriptionApiItem(
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("user_login")] string UserLogin,
        [property: JsonPropertyName("user_name")] string UserName);
}
