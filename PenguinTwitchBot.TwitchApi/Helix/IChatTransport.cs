using PenguinTwitchBot.TwitchApi.Models.Chat;

namespace PenguinTwitchBot.TwitchApi.Helix;

public interface IChatTransport
{
    Task<SendChatMessageResponse> SendChatMessageAsync(string clientId, string? accessToken, SendChatMessageRequest request);
    Task<GetChattersPageResponse> GetChattersAsync(string clientId, string? accessToken, string broadcasterId, string moderatorId, string? after);
    Task SendShoutoutAsync(string clientId, string? accessToken, string fromBroadcasterId, string toBroadcasterId, string moderatorId);
    Task SendChatAnnouncementAsync(string clientId, string? accessToken, string broadcasterId, string moderatorId, string message);
    Task<IReadOnlyList<ChatBadgeSet>> GetGlobalChatBadgesAsync(string clientId, string? accessToken);
    Task<IReadOnlyList<ChatBadgeSet>> GetChannelChatBadgesAsync(string clientId, string? accessToken, string broadcasterId);
}
