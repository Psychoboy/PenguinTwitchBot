using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.GetCustomReward;
using PenguinTwitchBot.TwitchApi.Models.ChannelPoints;

namespace PenguinTwitchBot.TwitchApi.Helix;

public interface IChannelPointsTransport
{
    Task<GetCustomRewardsResponse> GetCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, List<string>? rewardIds = null, bool onlyManageableRewards = false);
    Task<CreateCustomRewardsResponse> CreateCustomRewardsAsync(string clientId, string? accessToken, string broadcasterId, CreateCustomRewardsRequest request);
    Task UpdateCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, string rewardId, UpdateCustomRewardRequest request);
    Task DeleteCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, string rewardId);
}
