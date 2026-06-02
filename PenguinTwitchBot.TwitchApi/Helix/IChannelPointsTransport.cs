using PenguinTwitchBot.TwitchApi.Models.ChannelPoints;

namespace PenguinTwitchBot.TwitchApi.Helix;

public interface IChannelPointsTransport
{
    Task<GetChannelPointRewardsResponse> GetCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, List<string>? rewardIds = null, bool onlyManageableRewards = false);
    Task CreateCustomRewardsAsync(string clientId, string? accessToken, string broadcasterId, CreateChannelPointRewardRequest request);
    Task UpdateCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, string rewardId, UpdateCustomRewardRequest request);
    Task DeleteCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, string rewardId);
}
