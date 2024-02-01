using DotNetTwitchBot.Bot.TwitchServices;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;

namespace DotNetTwitchBot.Bot.Commands.ChannelPoints
{
    public class ChannelPoints(ITwitchService twitchService) : IChannelPoints
    {
        public Task<IEnumerable<CustomReward>> GetAllChannelPoints()
        {
            return twitchService.GetChannelPointRewards();
        }

        public Task<IEnumerable<CustomReward>> GetOwnedChannelPoints()
        {
            return twitchService.GetChannelPointRewards(true);
        }


        public Task CreateChannelPointReward(CreateCustomRewardsRequest request)
        {
            return twitchService.CreateChannelPointReward(request);
        }

        public Task DeleteChannelPointReward(string channelPointRewardId)
        {
            return twitchService.DeleteChannelPointReward(channelPointRewardId);
        }

        public Task UpdateChannelPointReward(string rewardId, UpdateCustomRewardRequest request)
        {
            return twitchService.UpdateChannelPointReward(rewardId, request);
        }
    }
}
