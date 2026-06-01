using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Subscriptions;

namespace PenguinTwitchBot.Bot.Twitch.Helix;

public sealed class SubscriptionsTransport : ISubscriptionsTransport
{
    public Task<CheckUserSubscriptionResponse> CheckUserSubscriptionAsync(string clientId, string? accessToken, string broadcasterId, string userId)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.Subscriptions.CheckUserSubscriptionAsync(broadcasterId, userId, accessToken);
    }

    public Task<GetBroadcasterSubscriptionsResponse> GetBroadcasterSubscriptionsAsync(string clientId, string? accessToken, string broadcasterId, int first, string? after)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.Subscriptions.GetBroadcasterSubscriptionsAsync(broadcasterId, first, after, accessToken: accessToken);
    }

    private static TwitchAPI CreateApi(string clientId, string? accessToken)
    {
        var api = new TwitchAPI();
        api.Settings.ClientId = clientId;
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            api.Settings.AccessToken = accessToken;
        }

        return api;
    }
}
