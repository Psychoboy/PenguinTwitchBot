using PenguinTwitchBot.Bot.Twitch.Models;
using PenguinTwitchBot.Bot.TwitchServices;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;

namespace PenguinTwitchBot.Bot.Commands.ChannelPoints
{
    public class ChannelPoints(ITwitchService twitchService) : IChannelPoints
    {
        public Task<IEnumerable<ChannelPointReward>> GetAllChannelPoints()
        {
            return twitchService.GetChannelPointRewards();
        }

        public Task<IEnumerable<ChannelPointReward>> GetOwnedChannelPoints()
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
