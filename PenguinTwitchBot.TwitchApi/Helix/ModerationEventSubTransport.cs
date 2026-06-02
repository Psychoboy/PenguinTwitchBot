using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.EventSub;
using TwitchLib.Api.Helix.Models.Moderation.BanUser;
using TwitchLib.Api.Helix.Models.Moderation.CheckAutoModStatus;
using TwitchLib.Api.Helix.Models.Moderation.GetBannedUsers;
using TwitchLib.Api.Helix.Models.Moderation.GetModerators;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class ModerationTransport : IModerationTransport
{
    public Task<CheckAutoModStatusResponse> CheckAutoModStatusAsync(string clientId, string? accessToken, List<Message> messages, string broadcasterId)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.Moderation.CheckAutoModStatusAsync(messages, broadcasterId, accessToken);
    }

    public Task<GetBannedUsersResponse> GetBannedUsersAsync(string clientId, string? accessToken, string broadcasterId, string? after)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.Moderation.GetBannedUsersAsync(broadcasterId: broadcasterId, first: 100, after: after, accessToken: accessToken);
    }

    public Task<GetModeratorsResponse> GetModeratorsAsync(string clientId, string? accessToken, string broadcasterId, List<string> userIds)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.Moderation.GetModeratorsAsync(broadcasterId, userIds, accessToken: accessToken);
    }

    public Task BanUserAsync(string clientId, string? accessToken, string broadcasterId, string moderatorId, BanUserRequest request)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.Moderation.BanUserAsync(broadcasterId, moderatorId, request, accessToken);
    }

    public Task DeleteChatMessagesAsync(string clientId, string? accessToken, string broadcasterId, string moderatorId, string? messageId)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.Moderation.DeleteChatMessagesAsync(broadcasterId, moderatorId, messageId, accessToken);
    }

    public Task<CreateEventSubSubscriptionResponse> CreateEventSubSubscriptionAsync(string clientId, string? accessToken, string type, string version, Dictionary<string, string> condition, EventSubTransportMethod transportMethod, string transportSessionId)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.EventSub.CreateEventSubSubscriptionAsync(type, version, condition, transportMethod, transportSessionId, accessToken);
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
