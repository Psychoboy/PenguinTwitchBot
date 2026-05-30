using TwitchLib.Api.Helix.Models.Subscriptions;

namespace PenguinTwitchBot.Bot.Twitch.Helix;

public sealed class SubscriptionsClient(ILogger<SubscriptionsClient> logger, ISubscriptionsTransport transport) : TwitchClientRetryBase(logger), ISubscriptionsClient
{
    public Task<CheckUserSubscriptionResponse> CheckUserSubscriptionAsync(string clientId, string? accessToken, string broadcasterId, string userId)
    {
        return ExecuteWithRetryAsync(() => transport.CheckUserSubscriptionAsync(clientId, accessToken, broadcasterId, userId), "check user subscription");
    }

    public Task<GetBroadcasterSubscriptionsResponse> GetBroadcasterSubscriptionsAsync(string clientId, string? accessToken, string broadcasterId, int first, string? after)
    {
        return ExecuteWithRetryAsync(() => transport.GetBroadcasterSubscriptionsAsync(clientId, accessToken, broadcasterId, first, after), "fetch broadcaster subscriptions");
    }
}
