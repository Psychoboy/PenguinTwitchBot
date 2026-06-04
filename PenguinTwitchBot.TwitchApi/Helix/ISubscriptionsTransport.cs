using PenguinTwitchBot.TwitchApi.Models.Subscriptions;

namespace PenguinTwitchBot.TwitchApi.Helix;

public interface ISubscriptionsTransport
{
    Task<CheckUserSubscriptionResponse> CheckUserSubscriptionAsync(string clientId, string? accessToken, string broadcasterId, string userId);
    Task<GetBroadcasterSubscriptionsResponse> GetBroadcasterSubscriptionsAsync(string clientId, string? accessToken, string broadcasterId, int first, string? after);
}
