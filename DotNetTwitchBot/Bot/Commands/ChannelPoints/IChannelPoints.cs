using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;

namespace DotNetTwitchBot.Bot.Commands.ChannelPoints
{
    public interface IChannelPoints
    {
        Task CreateChannelPointReward(CreateCustomRewardsRequest request);
        Task DeleteChannelPointReward(string channelPointRewardId);
        Task<IEnumerable<CustomReward>> GetAllChannelPoints();
        Task<IEnumerable<CustomReward>> GetOwnedChannelPoints();
        Task UpdateChannelPointReward(string rewardId, UpdateCustomRewardRequest request);
    }
}