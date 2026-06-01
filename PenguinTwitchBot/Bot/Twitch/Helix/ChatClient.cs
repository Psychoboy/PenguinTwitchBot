using TwitchLib.Api.Helix.Models.Channels.SendChatMessage;
using TwitchLib.Api.Helix.Models.Chat.Badges.GetChannelChatBadges;
using TwitchLib.Api.Helix.Models.Chat.Badges.GetGlobalChatBadges;
using TwitchLib.Api.Helix.Models.Chat.GetChatters;
using TwitchLibChatter = TwitchLib.Api.Helix.Models.Chat.GetChatters.Chatter;

namespace PenguinTwitchBot.Bot.Twitch.Helix;

public sealed class ChatClient(ILogger<ChatClient> logger, IChatTransport transport) : TwitchClientRetryBase(logger), IChatClient
{

    public Task<SendChatMessageResponse> SendChatMessageAsync(string clientId, string? accessToken, SendChatMessageRequest request)
    {
        return ExecuteWithRetryAsync(() => transport.SendChatMessageAsync(clientId, accessToken, request), "send chat message");
    }

    public Task<GetChattersResponse> GetChattersAsync(string clientId, string? accessToken, string broadcasterId, string moderatorId, string? after)
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

    internal static Models.Chatter MapToChatter(TwitchLibChatter source) =>
        new(UserId: source.UserId, UserLogin: source.UserLogin);

    public Task<GetGlobalChatBadgesResponse> GetGlobalChatBadgesAsync(string clientId, string? accessToken)
    {
        return ExecuteWithRetryAsync(() => transport.GetGlobalChatBadgesAsync(clientId, accessToken), "fetch global chat badges");
    }

    public Task<GetChannelChatBadgesResponse> GetChannelChatBadgesAsync(string clientId, string? accessToken, string broadcasterId)
    {
        return ExecuteWithRetryAsync(() => transport.GetChannelChatBadgesAsync(clientId, accessToken, broadcasterId), "fetch channel chat badges");
    }
}
