using PenguinTwitchBot.Bot.Twitch.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;

namespace PenguinTwitchBot.Bot.Commands.ChannelPoints
{
    public interface IChannelPoints
    {
        Task CreateChannelPointReward(CreateChannelPointRewardRequest request);
        Task DeleteChannelPointReward(string channelPointRewardId);
        Task<IEnumerable<ChannelPointReward>> GetAllChannelPoints();
        Task<IEnumerable<ChannelPointReward>> GetOwnedChannelPoints();
        Task UpdateChannelPointReward(string rewardId, UpdateCustomRewardRequest request);
    }
}