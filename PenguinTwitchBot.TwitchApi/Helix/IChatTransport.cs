using TwitchLib.Api.Helix.Models.Chat.Badges.GetChannelChatBadges;
using TwitchLib.Api.Helix.Models.Chat.Badges.GetGlobalChatBadges;
using TwitchLib.Api.Helix.Models.Chat.GetChatters;
using PenguinTwitchBot.TwitchApi.Models.Chat;

namespace PenguinTwitchBot.TwitchApi.Helix;

public interface IChatTransport
{
    Task<SendChatMessageResponse> SendChatMessageAsync(string clientId, string? accessToken, SendChatMessageRequest request);
    Task<GetChattersResponse> GetChattersAsync(string clientId, string? accessToken, string broadcasterId, string moderatorId, string? after);
    Task SendShoutoutAsync(string clientId, string? accessToken, string fromBroadcasterId, string toBroadcasterId, string moderatorId);
    Task SendChatAnnouncementAsync(string clientId, string? accessToken, string broadcasterId, string moderatorId, string message);
    Task<GetGlobalChatBadgesResponse> GetGlobalChatBadgesAsync(string clientId, string? accessToken);
    Task<GetChannelChatBadgesResponse> GetChannelChatBadgesAsync(string clientId, string? accessToken, string broadcasterId);
}
