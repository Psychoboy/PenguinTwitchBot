using PenguinTwitchBot.TwitchApi.Models.Channels;

namespace PenguinTwitchBot.TwitchApi.Helix;

public interface IChannelsClient
{
    Task<GetChannelInformationResponse> GetChannelInformationAsync(string clientId, string? accessToken, string broadcasterId);
    Task<GetChannelFollowersResponse> GetChannelFollowersAsync(string clientId, string? accessToken, string broadcasterId, string userId, int first, string? after);
    Task<GetChannelEditorsResponse> GetChannelEditorsAsync(string clientId, string? accessToken, string broadcasterId);
}
