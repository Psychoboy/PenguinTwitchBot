using System.Text.Json.Serialization;
using PenguinTwitchBot.TwitchApi.Models.Subscriptions;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class SubscriptionsTransport : ISubscriptionsTransport
{
    private readonly IHttpClientFactory _httpClientFactory;

    public SubscriptionsTransport(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<CheckUserSubscriptionResponse> CheckUserSubscriptionAsync(string clientId, string? accessToken, string broadcasterId, string userId)
    {
        using var http = HelixHttp.CreateClient(_httpClientFactory, clientId, accessToken);
        var url = HelixQuery.Build("subscriptions/user", new (string Key, string? Value)[]
        {
            ("broadcaster_id", broadcasterId),
            ("user_id", userId)
        });
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<HelixDataResponse<SubscriptionApiItem>>(response);
        var subscriptions = payload?.Data.Select(MapToSubscription).ToList() ?? [];
        return new CheckUserSubscriptionResponse(subscriptions);
    }

    public async Task<GetBroadcasterSubscriptionsResponse> GetBroadcasterSubscriptionsAsync(string clientId, string? accessToken, string broadcasterId, int first, string? after, CancellationToken cancellationToken = default)
    {
        using var http = HelixHttp.CreateClient(_httpClientFactory, clientId, accessToken);
        var url = BuildBroadcasterSubscriptionsUrl(broadcasterId, first, after);
        using var response = await http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<GetBroadcasterSubscriptionsApiResponse>(response, cancellationToken)
            ?? throw new InvalidOperationException("Twitch broadcaster subscriptions response payload was empty.");
        var subscriptions = (payload.Data ?? []).Select(MapToSubscription).ToList();
        return new GetBroadcasterSubscriptionsResponse(
            Data: subscriptions,
            Cursor: payload.Pagination?.Cursor,
            Total: payload.Total,
            Points: payload.Points);
    }

    private static string BuildBroadcasterSubscriptionsUrl(string broadcasterId, int first, string? after)
    {
        return HelixQuery.Build("subscriptions", new (string Key, string? Value)[]
        {
            ("broadcaster_id", broadcasterId),
            ("first", first.ToString()),
            ("after", after)
        });
    }

    private static Subscription MapToSubscription(SubscriptionApiItem source)
    {
        return new Subscription(
            UserId: source.UserId,
            UserLogin: source.UserLogin,
            UserName: source.UserName,
            Tier: source.Tier ?? string.Empty,
            IsGift: source.IsGift,
            GifterId: source.GifterId,
            GifterLogin: source.GifterLogin,
            GifterName: source.GifterName,
            PlanName: source.PlanName ?? string.Empty);
    }

    private sealed record GetBroadcasterSubscriptionsApiResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<SubscriptionApiItem> Data,
        [property: JsonPropertyName("pagination")] HelixPagination? Pagination,
        [property: JsonPropertyName("total")] int Total,
        [property: JsonPropertyName("points")] int Points);

    private sealed record SubscriptionApiItem(
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("user_login")] string UserLogin,
        [property: JsonPropertyName("user_name")] string UserName,
        [property: JsonPropertyName("tier")] string? Tier = null,
        [property: JsonPropertyName("is_gift")] bool IsGift = false,
        [property: JsonPropertyName("gifter_id")] string? GifterId = null,
        [property: JsonPropertyName("gifter_login")] string? GifterLogin = null,
        [property: JsonPropertyName("gifter_name")] string? GifterName = null,
        [property: JsonPropertyName("plan_name")] string? PlanName = null);
}
