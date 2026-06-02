using PenguinTwitchBot.TwitchApi.Models.ChannelPoints;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class ChannelPointsClient(ILogger<ChannelPointsClient> logger, IChannelPointsTransport transport) : TwitchClientRetryBase(logger), IChannelPointsClient
{

    public Task<GetChannelPointRewardsResponse> GetCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, List<string>? rewardIds = null, bool onlyManageableRewards = false)
    {
        return ExecuteWithRetryAsync(() => transport.GetCustomRewardAsync(clientId, accessToken, broadcasterId, rewardIds, onlyManageableRewards), "fetch custom rewards");
    }

    public Task CreateCustomRewardsAsync(string clientId, string? accessToken, string broadcasterId, CreateChannelPointRewardRequest request)
    {
        return ExecuteWithRetryAsync(() => transport.CreateCustomRewardsAsync(clientId, accessToken, broadcasterId, request), "create custom reward");
    }

    public Task UpdateCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, string rewardId, UpdateCustomRewardRequest request)
    {
        return ExecuteWithRetryAsync(() => transport.UpdateCustomRewardAsync(clientId, accessToken, broadcasterId, rewardId, request), "update custom reward");
    }

    public Task DeleteCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, string rewardId)
    {
        return ExecuteWithRetryAsync(() => transport.DeleteCustomRewardAsync(clientId, accessToken, broadcasterId, rewardId), "delete custom reward");
    }
}
