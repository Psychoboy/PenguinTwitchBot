using PenguinTwitchBot.Bot.Twitch.Models.ChannelPoints;
using PenguinTwitchBot.Bot.Twitch.Models.Channels;
using PenguinTwitchBot.Bot.Twitch.Models.Chat;
using PenguinTwitchBot.Bot.Twitch.Models.Clips;
using PenguinTwitchBot.Bot.Twitch.Models.Games;
using PenguinTwitchBot.Bot.Twitch.Models.Moderation;
using PenguinTwitchBot.Bot.Twitch.Models.Schedule;
using PenguinTwitchBot.Bot.Twitch.Models.Subscriptions;
using PenguinTwitchBot.Bot.Twitch.Models.Users;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;

namespace PenguinTwitchBot.Bot.TwitchServices
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
        Task<List<BannedUser>> GetAllBannedViewers();
        bool IsServiceUp();
        Task<IEnumerable<Chatter>> GetCurrentChatters();
        Task<IEnumerable<ChannelPointReward>> GetChannelPointRewards();
        Task UpdateChannelPointReward(string rewardId, UpdateCustomRewardRequest request);
        Task<IEnumerable<ChannelPointReward>> GetChannelPointRewards(bool onlyManageable);
        Task CreateChannelPointReward(CreateChannelPointRewardRequest request);
        Task DeleteChannelPointReward(string rewardId);
        Task<bool> WillBePermittedByAutomod(string message);
        Task<ChannelPointReward?> GetCustomReward(string id);
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
        Task SendMesssageAsStreamer(string message);
        Task<string> GetUserBio(string userId);
        Task<string> GetUserStreamTitle(string userId);
        /// <summary>
        /// Returns a flat dictionary mapping "setId/versionId" to the badge image URL (1x).
        /// Combines global and channel-specific badges; channel badges override globals.
        /// </summary>
        Task<Dictionary<string, string>> GetChatBadgesAsync();
    }
}