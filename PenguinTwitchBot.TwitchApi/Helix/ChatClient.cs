using PenguinTwitchBot.TwitchApi.Models.Chat;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class ChatClient(ILogger<ChatClient> logger, IChatTransport transport) : TwitchClientRetryBase(logger), IChatClient
{

    public Task<SendChatMessageResponse> SendChatMessageAsync(string clientId, string? accessToken, SendChatMessageRequest request)
    {
        return ExecuteWithRetryAsync(() => transport.SendChatMessageAsync(clientId, accessToken, request), "send chat message");
    }

    public Task<GetChattersPageResponse> GetChattersAsync(string clientId, string? accessToken, string broadcasterId, string moderatorId, string? after)
    {
        return ExecuteWithRetryAsync(() => transport.GetChattersAsync(clientId, accessToken, broadcasterId, moderatorId, after), "fetch chatters");
    }

    public Task SendShoutoutAsync(string clientId, string? accessToken, string fromBroadcasterId, string toBroadcasterId, string moderatorId)
    {
        return ExecuteWithRetryAsync(() => transport.SendShoutoutAsync(clientId, accessToken, fromBroadcasterId, toBroadcasterId, moderatorId), "send shoutout");
    }

    public Task SendChatAnnouncementAsync(string clientId, string? accessToken, string broadcasterId, string moderatorId, string message)
    {
        return ExecuteWithRetryAsync(() => transport.SendChatAnnouncementAsync(clientId, accessToken, broadcasterId, moderatorId, message), "send chat announcement");
    }

    public Task<IReadOnlyList<ChatBadgeSet>> GetGlobalChatBadgesAsync(string clientId, string? accessToken)
    {
        return ExecuteWithRetryAsync(() => transport.GetGlobalChatBadgesAsync(clientId, accessToken), "fetch global chat badges");
    }

    public Task<IReadOnlyList<ChatBadgeSet>> GetChannelChatBadgesAsync(string clientId, string? accessToken, string broadcasterId)
    {
        return ExecuteWithRetryAsync(() => transport.GetChannelChatBadgesAsync(clientId, accessToken, broadcasterId), "fetch channel chat badges");
    }
}
