using PenguinTwitchBot.Bot.Twitch.Models;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;

namespace PenguinTwitchBot.Bot.Commands.ChannelPoints
{
    public interface IChannelPoints
    {
        Task CreateChannelPointReward(CreateCustomRewardsRequest request);
        Task DeleteChannelPointReward(string channelPointRewardId);
        Task<IEnumerable<ChannelPointReward>> GetAllChannelPoints();
        Task<IEnumerable<ChannelPointReward>> GetOwnedChannelPoints();
        Task UpdateChannelPointReward(string rewardId, UpdateCustomRewardRequest request);
    }
}