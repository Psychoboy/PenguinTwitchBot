using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;
using TwitchLib.Api.Helix.Models.Channels.GetChannelEditors;
using TwitchLib.Api.Helix.Models.Channels.GetChannelInformation;
using TwitchLib.Api.Helix.Models.Chat.GetChatters;
using TwitchLib.Api.Helix.Models.Clips.GetClips;
using TwitchLib.Api.Helix.Models.Games;
using TwitchLib.Api.Helix.Models.Moderation.GetBannedUsers;
using TwitchLib.Api.Helix.Models.Schedule;
using TwitchLib.Api.Helix.Models.Subscriptions;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace DotNetTwitchBot.Bot.TwitchServices
{
    public interface ITwitchService
    {
        Task Announcement(string message);
        Task<List<TwitchModels.OnlineStream>> AreStreamsOnline(List<string> userIds);
        Task<List<Subscription>> GetAllSubscriptions();
        Task<string?> GetBotUserId();
        Task<string?> GetBroadcasterUserId();
        Task<string> GetCurrentGame();
        Task<string> GetCurrentGame(string userId);
        Task<string> GetStreamThumbnail();
        Task<string> GetStreamTitle();
        Task<User?> GetUserByName(string user);
        Task<User?> GetUserById(string userId);
        Task<Follower?> GetUserFollow(string user);
        Task<string?> GetUserId(string user);
        Task<int> GetViewerCount();
        Task<bool> IsStreamOnline();
        Task<bool> IsStreamOnline(string userId);
        Task<bool> IsUserMod(string user);
        Task<bool> IsUserSub(string user);
        Task RaidStreamer(string userId);
        Task<ShoutoutResponseEnum> ShoutoutStreamer(string userId);
        Task<DateTime> StreamStartedAt();
        Task<bool> SubscribeToAllTheStuffs(string sessionId);
        Task TimeoutUser(string name, string reason, int? length);
        Task<bool> ValidateAndRefreshToken();
        Task<List<BannedUserEvent>> GetAllBannedViewers();
        bool IsServiceUp();
        Task<IEnumerable<Chatter>> GetCurrentChatters();
        Task<IEnumerable<CustomReward>> GetChannelPointRewards();
        Task UpdateChannelPointReward(string rewardId, UpdateCustomRewardRequest request);
        Task<IEnumerable<CustomReward>> GetChannelPointRewards(bool onlyManageable);
        Task<CreateCustomRewardsResponse> CreateChannelPointReward(CreateCustomRewardsRequest request);
        Task DeleteChannelPointReward(string rewardId);
        Task<bool> WillBePermittedByAutomod(string message);
        Task<CustomReward?> GetCustomReward(string id);
        void SetAccessToken(string accessToken);
        Task SendMessage(string message);
        Task<ChannelStreamSchedule?> GetStreamSchedule();
        Task<List<Clip>> GetFeaturedClips(string user);
        Task<List<Clip>> GetClips(string user);
        Task<List<Clip>> GetClip(string clipId);
        Task<ChannelInformation?> GetChannelInfo(string userId);
        Task<Game?> GetGameInfo(string gameId);
        Task<List<User?>?> GetUsers(List<string> userNames);
        Task<List<ChannelEditor>> GetEditors();
        Task DeleteMessage(string messageId);
    }
}