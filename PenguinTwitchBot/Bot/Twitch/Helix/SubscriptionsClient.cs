using TwitchLib.Api.Helix.Models.Subscriptions;
using TwitchLibSubscription = TwitchLib.Api.Helix.Models.Subscriptions.Subscription;

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

    internal static Models.Subscriptions.Subscription MapToSubscription(TwitchLibSubscription source) =>
        new(UserId: source.UserId, UserLogin: source.UserLogin, UserName: source.UserName);
}
