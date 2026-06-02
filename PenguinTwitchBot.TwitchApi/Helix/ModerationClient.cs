using PenguinTwitchBot.TwitchApi.Models.EventSub;
using PenguinTwitchBot.TwitchApi.Models.Moderation;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class ModerationClient(ILogger<ModerationClient> logger, IModerationTransport transport) : TwitchClientRetryBase(logger), IModerationClient
{

    public Task<CheckAutoModStatusResponse> CheckAutoModStatusAsync(string clientId, string? accessToken, List<AutoModMessage> messages, string broadcasterId)
    {
        return ExecuteWithRetryAsync(() => transport.CheckAutoModStatusAsync(clientId, accessToken, messages, broadcasterId), "check automod status");
    }

    public Task<GetBannedUsersResponse> GetBannedUsersAsync(string clientId, string? accessToken, string broadcasterId, string? after)
    {
        return ExecuteWithRetryAsync(() => transport.GetBannedUsersAsync(clientId, accessToken, broadcasterId, after), "fetch banned users");
    }

    public Task<GetModeratorsResponse> GetModeratorsAsync(string clientId, string? accessToken, string broadcasterId, List<string> userIds)
    {
        return ExecuteWithRetryAsync(() => transport.GetModeratorsAsync(clientId, accessToken, broadcasterId, userIds), "fetch moderators");
    }

    public Task BanUserAsync(string clientId, string? accessToken, string broadcasterId, string moderatorId, BanUserRequest request)
    {
        return ExecuteWithRetryAsync(() => transport.BanUserAsync(clientId, accessToken, broadcasterId, moderatorId, request), "ban user");
    }

    public Task DeleteChatMessagesAsync(string clientId, string? accessToken, string broadcasterId, string moderatorId, string? messageId)
    {
        return ExecuteWithRetryAsync(() => transport.DeleteChatMessagesAsync(clientId, accessToken, broadcasterId, moderatorId, messageId), "delete chat messages");
    }

    public async Task<EventSubSubscriptionResult> CreateEventSubSubscriptionAsync(string clientId, string? accessToken, string type, string version, Dictionary<string, string> condition, Models.EventSub.EventSubTransportMethod transportMethod, string transportSessionId)
    {
        return await ExecuteWithRetryAsync(
            () => transport.CreateEventSubSubscriptionAsync(clientId, accessToken, type, version, condition, transportMethod, transportSessionId),
            "create eventsub subscription");
    }
}
