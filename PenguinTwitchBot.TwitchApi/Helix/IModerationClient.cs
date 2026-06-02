using PenguinTwitchBot.TwitchApi.Models.EventSub;
using PenguinTwitchBot.TwitchApi.Models.Moderation;

namespace PenguinTwitchBot.TwitchApi.Helix;

public interface IModerationClient
{
    Task<CheckAutoModStatusResponse> CheckAutoModStatusAsync(string clientId, string? accessToken, List<AutoModMessage> messages, string broadcasterId);
    Task<GetBannedUsersResponse> GetBannedUsersAsync(string clientId, string? accessToken, string broadcasterId, string? after);
    Task<GetModeratorsResponse> GetModeratorsAsync(string clientId, string? accessToken, string broadcasterId, List<string> userIds);
    Task BanUserAsync(string clientId, string? accessToken, string broadcasterId, string moderatorId, BanUserRequest request);
    Task DeleteChatMessagesAsync(string clientId, string? accessToken, string broadcasterId, string moderatorId, string? messageId);
    Task<EventSubSubscriptionResult> CreateEventSubSubscriptionAsync(string clientId, string? accessToken, string type, string version, Dictionary<string, string> condition, EventSubTransportMethod transportMethod, string transportSessionId);
}
