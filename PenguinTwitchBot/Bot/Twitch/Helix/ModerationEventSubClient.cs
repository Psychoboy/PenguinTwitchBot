using PenguinTwitchBot.Bot.Twitch.Models.EventSub;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.EventSub;
using TwitchLib.Api.Helix.Models.Moderation.BanUser;
using TwitchLib.Api.Helix.Models.Moderation.CheckAutoModStatus;
using TwitchLib.Api.Helix.Models.Moderation.GetBannedUsers;
using TwitchLib.Api.Helix.Models.Moderation.GetModerators;
using TwitchLibBannedUserEvent = TwitchLib.Api.Helix.Models.Moderation.GetBannedUsers.BannedUserEvent;
using TwitchLibEventSubTransportMethod = TwitchLib.Api.Core.Enums.EventSubTransportMethod;

namespace PenguinTwitchBot.Bot.Twitch.Helix;

public sealed class ModerationEventSubClient(ILogger<ModerationEventSubClient> logger, IModerationEventSubTransport transport) : TwitchClientRetryBase(logger), IModerationEventSubClient
{

    public Task<CheckAutoModStatusResponse> CheckAutoModStatusAsync(string clientId, string? accessToken, List<Message> messages, string broadcasterId)
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
        var twitchMethod = transportMethod == Models.EventSub.EventSubTransportMethod.Websocket
            ? TwitchLibEventSubTransportMethod.Websocket
            : throw new ArgumentOutOfRangeException(nameof(transportMethod), transportMethod, null);
        var response = await ExecuteWithRetryAsync(
            () => transport.CreateEventSubSubscriptionAsync(clientId, accessToken, type, version, condition, twitchMethod, transportSessionId),
            "create eventsub subscription");
        var isEnabled = response?.Subscriptions?.Length > 0 && response.Subscriptions.First().Status == "enabled";
        return new EventSubSubscriptionResult(isEnabled);
    }

    internal static Models.Moderation.BannedUser MapToBannedUser(TwitchLibBannedUserEvent source) =>
        new(UserId: source.UserId, UserLogin: source.UserLogin, ExpiresAt: source.ExpiresAt);
}
