using TwitchLib.Api;
using PenguinTwitchBot.TwitchApi.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.GetCustomReward;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class ChannelPointsTransport : IChannelPointsTransport
{
    public Task<GetCustomRewardsResponse> GetCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, List<string>? rewardIds = null, bool onlyManageableRewards = false)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.ChannelPoints.GetCustomRewardAsync(broadcasterId, rewardIds, onlyManageableRewards, accessToken);
    }

    public Task<CreateCustomRewardsResponse> CreateCustomRewardsAsync(string clientId, string? accessToken, string broadcasterId, CreateCustomRewardsRequest request)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.ChannelPoints.CreateCustomRewardsAsync(broadcasterId, request, accessToken);
    }

    public Task UpdateCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, string rewardId, UpdateCustomRewardRequest request)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.ChannelPoints.UpdateCustomRewardAsync(
            broadcasterId,
            rewardId,
            ChannelPointsClient.MapToTwitchRequest(request),
            accessToken);
    }

    public Task DeleteCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, string rewardId)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.ChannelPoints.DeleteCustomRewardAsync(broadcasterId, rewardId, accessToken);
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
