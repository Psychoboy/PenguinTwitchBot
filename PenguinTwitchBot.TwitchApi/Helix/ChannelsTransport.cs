using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Channels.GetChannelEditors;
using TwitchLib.Api.Helix.Models.Channels.GetChannelFollowers;
using TwitchLib.Api.Helix.Models.Channels.GetChannelInformation;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class ChannelsTransport : IChannelsTransport
{
    public Task<GetChannelInformationResponse> GetChannelInformationAsync(string clientId, string? accessToken, string broadcasterId)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.Channels.GetChannelInformationAsync(broadcasterId, accessToken);
    }

    public Task<GetChannelFollowersResponse> GetChannelFollowersAsync(string clientId, string? accessToken, string broadcasterId, string userId, int first, string? after)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.Channels.GetChannelFollowersAsync(broadcasterId, userId, first, after, accessToken);
    }

    public Task<GetChannelEditorsResponse> GetChannelEditorsAsync(string clientId, string? accessToken, string broadcasterId)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.Channels.GetChannelEditorsAsync(broadcasterId, accessToken);
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
