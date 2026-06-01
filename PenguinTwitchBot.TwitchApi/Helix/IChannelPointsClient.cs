using PenguinTwitchBot.TwitchApi.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.GetCustomReward;

namespace PenguinTwitchBot.TwitchApi.Helix;

public interface IChannelPointsClient
{
    Task<GetCustomRewardsResponse> GetCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, List<string>? rewardIds = null, bool onlyManageableRewards = false);
    Task CreateCustomRewardsAsync(string clientId, string? accessToken, string broadcasterId, CreateChannelPointRewardRequest request);
    Task UpdateCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, string rewardId, UpdateCustomRewardRequest request);
    Task DeleteCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, string rewardId);
}
