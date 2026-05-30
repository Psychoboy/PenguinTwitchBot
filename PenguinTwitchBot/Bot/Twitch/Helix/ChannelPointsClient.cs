using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.GetCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;

namespace PenguinTwitchBot.Bot.Twitch.Helix;

public sealed class ChannelPointsClient(ILogger<ChannelPointsClient> logger, IChannelPointsTransport transport) : TwitchClientRetryBase(logger), IChannelPointsClient
{

    public Task<GetCustomRewardsResponse> GetCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, List<string>? rewardIds = null, bool onlyManageableRewards = false)
    {
        return ExecuteWithRetryAsync(() => transport.GetCustomRewardAsync(clientId, accessToken, broadcasterId, rewardIds, onlyManageableRewards), "fetch custom rewards");
    }

    public Task<CreateCustomRewardsResponse> CreateCustomRewardsAsync(string clientId, string? accessToken, string broadcasterId, CreateCustomRewardsRequest request)
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
