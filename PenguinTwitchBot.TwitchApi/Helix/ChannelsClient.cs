using PenguinTwitchBot.TwitchApi.Models.Channels;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class ChannelsClient(ILogger<ChannelsClient> logger, IChannelsTransport transport) : TwitchClientRetryBase(logger), IChannelsClient
{
    public Task<GetChannelInformationResponse> GetChannelInformationAsync(string clientId, string? accessToken, string broadcasterId)
    {
        return ExecuteWithRetryAsync(() => transport.GetChannelInformationAsync(clientId, accessToken, broadcasterId), "fetch channel information");
    }

    public Task<GetChannelFollowersResponse> GetChannelFollowersAsync(string clientId, string? accessToken, string broadcasterId, string userId, int first, string? after)
    {
        return ExecuteWithRetryAsync(() => transport.GetChannelFollowersAsync(clientId, accessToken, broadcasterId, userId, first, after), "fetch channel followers");
    }

    public Task<GetChannelEditorsResponse> GetChannelEditorsAsync(string clientId, string? accessToken, string broadcasterId)
    {
        return ExecuteWithRetryAsync(() => transport.GetChannelEditorsAsync(clientId, accessToken, broadcasterId), "fetch channel editors");
    }
}
