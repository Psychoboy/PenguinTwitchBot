using TwitchLib.Api.Helix.Models.Channels.GetChannelEditors;
using TwitchLib.Api.Helix.Models.Channels.GetChannelFollowers;
using TwitchLib.Api.Helix.Models.Channels.GetChannelInformation;

namespace PenguinTwitchBot.Bot.Twitch.Helix;

public interface IChannelsTransport
{
    Task<GetChannelInformationResponse> GetChannelInformationAsync(string clientId, string? accessToken, string broadcasterId);
    Task<GetChannelFollowersResponse> GetChannelFollowersAsync(string clientId, string? accessToken, string broadcasterId, string userId, int first, string? after);
    Task<GetChannelEditorsResponse> GetChannelEditorsAsync(string clientId, string? accessToken, string broadcasterId);
}
