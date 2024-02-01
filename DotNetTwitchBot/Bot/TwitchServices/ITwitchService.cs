using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;
using TwitchLib.Api.Helix.Models.Chat.GetChatters;
using TwitchLib.Api.Helix.Models.Moderation.GetBannedUsers;
using TwitchLib.Api.Helix.Models.Subscriptions;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace DotNetTwitchBot.Bot.TwitchServices
{
    public interface ITwitchService
    {
        Task Announcement(string message);
        Task<List<string>> AreStreamsOnline(List<string> userIds);
        Task<List<Subscription>> GetAllSubscriptions();
        Task<string?> GetBotUserId();
        Task<string?> GetBroadcasterUserId();
        Task<string> GetCurrentGame();
        Task<string> GetCurrentGame(string userId);
        Task<string> GetStreamThumbnail();
        Task<string> GetStreamTitle();
        Task<User?> GetUser(string user);
        Task<Follower?> GetUserFollow(string user);
        Task<string?> GetUserId(string user);
        Task<int> GetViewerCount();
        Task<bool> IsStreamOnline();
        Task<bool> IsStreamOnline(string userId);
        Task<bool> IsUserMod(string user);
        Task<bool> IsUserSub(string user);
        Task RaidStreamer(string userId);
        Task ShoutoutStreamer(string userId);
        Task<DateTime> StreamStartedAt();
        Task SubscribeToAllTheStuffs(string sessionId);
        Task TimeoutUser(string name, int length, string reason);
        Task ValidateAndRefreshToken();
        Task<List<BannedUserEvent>> GetAllBannedViewers();
        bool IsServiceUp();
        Task<IEnumerable<Chatter>> GetCurrentChatters();
        Task<IEnumerable<CustomReward>> GetChannelPointRewards();
        Task UpdateChannelPointReward(string rewardId, UpdateCustomRewardRequest request);
        Task<IEnumerable<CustomReward>> GetChannelPointRewards(bool onlyManageable);
        Task<CreateCustomRewardsResponse> CreateChannelPointReward(CreateCustomRewardsRequest request);
        Task DeleteChannelPointReward(string rewardId);
    }
}