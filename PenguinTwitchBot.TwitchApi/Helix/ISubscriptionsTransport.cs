using TwitchLib.Api.Helix.Models.Subscriptions;

namespace PenguinTwitchBot.Bot.Twitch.Helix;

public interface ISubscriptionsTransport
{
    Task<CheckUserSubscriptionResponse> CheckUserSubscriptionAsync(string clientId, string? accessToken, string broadcasterId, string userId);
    Task<GetBroadcasterSubscriptionsResponse> GetBroadcasterSubscriptionsAsync(string clientId, string? accessToken, string broadcasterId, int first, string? after);
}
