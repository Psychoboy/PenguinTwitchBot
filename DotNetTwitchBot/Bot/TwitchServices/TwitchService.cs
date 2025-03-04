using System.Collections.Concurrent;
using System.Timers;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Core.Exceptions;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.Channels.GetChannelEditors;
using TwitchLib.Api.Helix.Models.Chat.GetChatters;
using TwitchLib.Api.Helix.Models.Moderation.GetBannedUsers;
using TwitchLib.Api.Helix.Models.Subscriptions;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.TwitchServices
{
    /// <summary>
    /// Everything here is executed as the streamer
    /// </summary>
    public class TwitchService : ITwitchService
    {
        private readonly TwitchAPI _twitchApi = new();
        private readonly ILogger<TwitchService> _logger;
        private readonly IConfiguration _configuration;
        static readonly SemaphoreSlim semaphoreSlim = new(1, 1);
        readonly ConcurrentDictionary<string, TwitchLib.Api.Helix.Models.Users.GetUsers.User?> UserCache = new();
        readonly Timer _timer;
        private readonly SettingsFileManager _settingsFileManager;
        private readonly ChatMessageIdTracker _messageIdTracker;
        private bool serviceUp = false;
        private string? broadcasterId = string.Empty;
        private string? botId = string.Empty;

        public TwitchService(
            ILogger<TwitchService> logger,
            IConfiguration configuration,
            SettingsFileManager settingsFileManager,
            ChatMessageIdTracker messageIdTracker)
        {

            _logger = logger;
            _settingsFileManager = settingsFileManager;
            _configuration = configuration;
            _twitchApi.Settings.ClientId = _configuration["twitchClientId"];
            _twitchApi.Settings.AccessToken = _configuration["twitchAccessToken"];
            _twitchApi.Settings.Scopes = [];
            _timer = new Timer();
            _timer = new Timer(300000); //5 minutes
            _timer.Elapsed += OnTimerElapsed;
            _timer.Start();
            _messageIdTracker = messageIdTracker;

            foreach (var authScope in Enum.GetValues(typeof(AuthScopes)))
            {
                if ((AuthScopes)authScope == AuthScopes.Any) continue;
                _twitchApi.Settings.Scopes.Add((AuthScopes)authScope);
            }

        }

        public void SetAccessToken(string accessToken)
        {
            _twitchApi.Settings.AccessToken = accessToken;
        }

        public bool IsServiceUp()
        {
            return serviceUp;
        }

        public async Task SendMessage(string message)
        {
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                var result = await _twitchApi.Helix.Chat.SendChatMessage(broadcasterId, broadcasterId, message);
                _messageIdTracker.AddMessageId(result.Data.First().MessageId);
                if (result.Data.First().IsSent == false)
                {
                    _logger.LogWarning("Message failed to send: {reason}", result.Data.First().DropReason.Message);
                }
                else
                {
                    _logger.LogInformation("STREAMERCHATMSG: {message}", message.Replace(Environment.NewLine, ""));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message.");
            }
        }

        public async Task<TwitchLib.Api.Helix.Models.Schedule.ChannelStreamSchedule?> GetStreamSchedule()
        {
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                var result = await _twitchApi.Helix.Schedule.GetChannelStreamScheduleAsync(broadcasterId);
                if (result != null)
                {
                    return result.Schedule;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stream schedule.");
            }
            return null;
        }

        private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            await ValidateAndRefreshToken();
        }

        public async Task<IEnumerable<Chatter>> GetCurrentChatters()
        {
            await ValidateAndRefreshToken();
            var broadcasterId = await GetBroadcasterUserId();
            var after = "";
            List<Chatter> chatters = [];
            try
            {
                while (true)
                {
                    var curChatters = await _twitchApi.Helix.Chat.GetChattersAsync(broadcasterId, broadcasterId, after: after, accessToken: _configuration["twitchAccessToken"]);
                    if (curChatters != null)
                    {
                        chatters.AddRange(curChatters.Data);
                        if (string.IsNullOrEmpty(curChatters.Pagination.Cursor))
                        {
                            break;
                        }
                        after = curChatters.Pagination.Cursor;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chatters.");
            }
            return chatters;
        }

        public async Task<CustomReward?> GetCustomReward(string id)
        {
            await ValidateAndRefreshToken();
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                var result = await _twitchApi.Helix.ChannelPoints.GetCustomRewardAsync(broadcasterId, [id], accessToken: _configuration["twitchAccessToken"]);
                return result == null
                    ? throw new BadParameterException("Result from Twitch API getting channel point reward was null")
                    : result.Data.First();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting custom reward.");
            }
            return null;
        }

        public async Task<bool> WillBePermittedByAutomod(string message)
        {
            await ValidateAndRefreshToken();
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                var result = await _twitchApi.Helix.Moderation.CheckAutoModStatusAsync(new List<TwitchLib.Api.Helix.Models.Moderation.CheckAutoModStatus.Message>
                {
                    new() {
                        MsgId = Guid.NewGuid().ToString(),
                        MsgText = message
                    }
                }, broadcasterId, _configuration["twitchAccessToken"]);
                if (result == null)
                {
                    _logger.LogWarning("Failed to check automod message.");
                    return true;
                }
                return result.Data.First().IsPermitted;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking automod.");
                return true;
            }
        }

        public async Task<CreateCustomRewardsResponse> CreateChannelPointReward(CreateCustomRewardsRequest request)
        {
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                return await _twitchApi.Helix.ChannelPoints.CreateCustomRewardsAsync(broadcasterId, request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating channel point reward");
                return new();
            }
        }

        public async Task<IEnumerable<TwitchLib.Api.Helix.Models.ChannelPoints.CustomReward>> GetChannelPointRewards()
        {
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                var rewards = await _twitchApi.Helix.ChannelPoints.GetCustomRewardAsync(broadcasterId);
                if (rewards != null)
                {
                    return rewards.Data.ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting channel point rewards.");
            }
            return [];
        }

        public async Task<IEnumerable<TwitchLib.Api.Helix.Models.ChannelPoints.CustomReward>> GetChannelPointRewards(bool onlyManageable)
        {
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                var rewards = await _twitchApi.Helix.ChannelPoints.GetCustomRewardAsync(broadcasterId, onlyManageableRewards: onlyManageable);
                if (rewards != null)
                {
                    return rewards.Data.ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting channel point rewards.");
            }
            return [];
        }

        public async Task UpdateChannelPointReward(string rewardId, TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward.UpdateCustomRewardRequest request)
        {
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                await _twitchApi.Helix.ChannelPoints.UpdateCustomRewardAsync(broadcasterId, rewardId, request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating channel point rewards.");
            }
        }

        public async Task DeleteChannelPointReward(string rewardId)
        {
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                await _twitchApi.Helix.ChannelPoints.DeleteCustomRewardAsync(broadcasterId, rewardId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting channel point rewards.");
            }
        }

        public async Task<List<Subscription>> GetAllSubscriptions()
        {
            await ValidateAndRefreshToken();
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting user id.");
            List<Subscription> subs = [];
            var after = "";
            while (true)
            {
                var curSubs = await _twitchApi.Helix.Subscriptions.GetBroadcasterSubscriptionsAsync(userId, 100, after, accessToken: _configuration["twitchAccessToken"]);
                if (curSubs != null)
                {
                    subs.AddRange(curSubs.Data);
                    if (string.IsNullOrEmpty(curSubs.Pagination.Cursor))
                    {
                        break;
                    }
                    after = curSubs.Pagination.Cursor;
                }
            }
            return subs;
        }

        public async Task<List<BannedUserEvent>> GetAllBannedViewers()
        {
            await ValidateAndRefreshToken();
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting user id.");
            var after = "";
            List<BannedUserEvent> curBannedUsers = [];
            while (true)
            {
                var bannedUsers = await _twitchApi.Helix.Moderation.GetBannedUsersAsync(broadcasterId: userId, first: 100, after: after, accessToken: _configuration["twitchAccessToken"]);
                if (bannedUsers != null)
                {
                    curBannedUsers.AddRange(bannedUsers.Data);
                    if (string.IsNullOrEmpty(bannedUsers.Pagination.Cursor))
                    {
                        break;
                    }
                    after = bannedUsers.Pagination.Cursor;
                }
            }
            return curBannedUsers;
        }

        public async Task<string?> GetBroadcasterUserId()
        {
            var broadcaster = _configuration["broadcaster"];
            if (broadcaster == null) return null;
            if (string.IsNullOrEmpty(broadcasterId))
            {
                broadcasterId = await GetUserId(broadcaster);
            }
            return broadcasterId;
        }

        public async Task<string?> GetBotUserId()
        {
            var bot = _configuration["botName"];
            if (bot == null) return null;
            if (string.IsNullOrEmpty(botId))
            {
                botId = await GetUserId(bot);
            }
            return botId;
        }

        public async Task<string?> GetUserId(string user)
        {
            return (await GetUserByName(user))?.Id;
        }

        public async Task<TwitchLib.Api.Helix.Models.Users.GetUsers.User?> GetUserByName(string user)
        {
            try
            {
                if (UserCache.TryGetValue(user, out var twitchUser))
                {
                    if (twitchUser != null)
                    {
                        return twitchUser;
                    }
                }

                var users = await _twitchApi.Helix.Users.GetUsersAsync(null, [user], _configuration["twitchAccessToken"]);
                var userObj = users.Users.FirstOrDefault();
                UserCache[user] = userObj;
                return userObj;
            }
            catch (TooManyRequestsException)
            {
                return null;
            }
            catch (Exception)
            {
                _logger.LogCritical("Failed getting user");
                return null;
            }
        }

        public async Task<TwitchLib.Api.Helix.Models.Users.GetUsers.User?> GetUserById(string userId)
        {
            try
            {

                var users = await _twitchApi.Helix.Users.GetUsersAsync([userId],null, _configuration["twitchAccessToken"]);
                var userObj = users.Users.FirstOrDefault();
                return userObj;
            }
            catch (TooManyRequestsException)
            {
                return null;
            }
            catch (Exception)
            {
                _logger.LogCritical("Failed getting user");
                return null;
            }
        }

        public async Task<List<TwitchLib.Api.Helix.Models.Users.GetUsers.User?>?> GetUsers(List<string> userNames)
        {
            try
            {

                var users = await _twitchApi.Helix.Users.GetUsersAsync(null, userNames, _configuration["twitchAccessToken"]);
                if (users == null) throw new Exception("Got a null response");
                foreach (var user in users.Users)
                {
                    UserCache[user.Login] = user;
                }
                return [.. users.Users];
            }
            catch (TooManyRequestsException)
            {
                return null;
            }
            catch (Exception)
            {
                _logger.LogCritical("Failed getting user");
                return null;
            }
        }

        public async Task<Follower?> GetUserFollow(string user)
        {
            var broadcasterId = await GetBroadcasterUserId();
            var userId = await GetUserId(user);
            if (userId == null) return null;
            if (broadcasterId == userId)
            {
                return new()
                {
                    UserId = userId,
                    DisplayName = user,
                    Username = user,
                    FollowDate = DateTime.Now
                };

            }
            try
            {
                //var response = await _twitchApi.Helix.Users.GetUsersFollowsAsync(null, null, 1, userId, broadcasterId, _configuration["twitchAccessToken"]);
                var response = await _twitchApi.Helix.Channels.GetChannelFollowersAsync(broadcasterId, userId, 1, null, _configuration["twitchAccessToken"]);
                if (response.Data.Length == 0)
                {
                    return null;
                }
                var firstFollower = response.Data[0];
                return new Follower()
                {
                    Username = firstFollower.UserLogin,
                    UserId = firstFollower.UserId,
                    DisplayName = firstFollower.UserName,
                    FollowDate = DateTime.Parse( firstFollower.FollowedAt)
                };
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing GetUserFollow():{user} {error}", user, error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetUserFollow():{user} {error}", user, error);
            }
            return null;
        }

        public async Task<bool> IsUserSub(string user)
        {
            var broadcasterId = await GetBroadcasterUserId();
            var userId = await GetUserId(user);
            if (userId == null) return false;
            try
            {
                var response = await _twitchApi.Helix.Subscriptions.CheckUserSubscriptionAsync(broadcasterId, userId, _configuration["twitchAccessToken"]);
                if (response == null) return false;
                return response.Data.Length != 0;
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing IsUserSub(): {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing IsUserSub(): {error}", error);
            }
            return false;
        }

        public async Task<bool> IsUserMod(string user)
        {
            var broadcasterId = await GetBroadcasterUserId();
            var userId = await GetUserId(user);
            if (userId == null) return false;
            try
            {
                var response = await _twitchApi.Helix.Moderation.GetModeratorsAsync(broadcasterId, [userId]);
                return response.Data.Length != 0;
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing IsUserMod(): {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing IsUserMod(): {error}", error);
            }
            return false;
        }

        private async Task<TwitchLib.Api.Helix.Models.Streams.GetStreams.GetStreamsResponse> GetStreams(string userId)
        {
            return await GetStreams(new List<string>() { userId });
        }

        private async Task<TwitchLib.Api.Helix.Models.Streams.GetStreams.GetStreamsResponse> GetStreams(List<string> userIds)
        {
            return await _twitchApi.Helix.Streams.GetStreamsAsync(userIds: userIds, first: 100, accessToken: _configuration["twitchAccessToken"]);
        }

        public async Task<bool> IsStreamOnline()
        {
            try
            {
                var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting stream status.");
                return await IsStreamOnline(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error doing IsStreamOnline()");
            }
            return false;
        }

        public async Task<bool> IsStreamOnline(string userId)
        {
            try
            {
                var streams = await GetStreams(userId);
                if (streams.Streams == null)
                {
                    return false;
                }

                return streams.Streams.Length > 0;
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing IsStreamOnline(userId): {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing IsStreamOnline(userId): {error}", error);
            }
            return false;
        }

        public async Task<List<TwitchModels.OnlineStream>> AreStreamsOnline(List<string> userIds)
        {
            try
            {
                var streams = await GetStreams(userIds);
                if (streams.Streams == null)
                {
                    return [];
                }
                return streams.Streams.Select(x => new TwitchModels.OnlineStream
                {
                    UserId = x.UserId,
                    UserName = x.UserName,
                    DisplayName = x.UserName,
                    Game = x.GameName
                }).ToList();
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing AreStreamsOnline: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing AreStreamsOnline: {error}", error);
            }
            return [];
        }

        public async Task<DateTime> StreamStartedAt()
        {
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting stream status.");
            try
            {
                var streams = await GetStreams(userId);
                if (streams.Streams.Length == 0)
                {
                    return DateTime.MinValue;
                }
                var stream = streams.Streams.First();
                var startTime = stream.StartedAt;
                return startTime;
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing getting stream started at: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing StreamStartedAt(): {error}", error);
            }
            return DateTime.MinValue;
        }

        public async Task<int> GetViewerCount()
        {
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting stream status.");
            try
            {
                var streams = await GetStreams(userId);
                if (streams.Streams.Length == 0)
                {
                    return 0;
                }
                return streams.Streams.First().ViewerCount;
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing getting viewer count: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetViewerCount(): {error}", error);
            }
            return 0;
        }

        public async Task<string> GetCurrentGame()
        {
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting stream status.");
            return await GetCurrentGame(userId);
        }

        private async Task<TwitchLib.Api.Helix.Models.Channels.GetChannelInformation.GetChannelInformationResponse> GetChannelInformation(string userId)
        {
            return await _twitchApi.Helix.Channels.GetChannelInformationAsync(userId, _configuration["twitchAccessToken"]);
        }

        public async Task<string> GetCurrentGame(string userId)
        {
            try
            {
                var channelInfo = await GetChannelInformation(userId);
                if (channelInfo.Data.Length > 0)
                {
                    return channelInfo.Data[0].GameName;
                }
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing Getting current game: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetCurrentGame(): {error}", error);
            }
            return "";
        }

        public async Task<TwitchLib.Api.Helix.Models.Channels.GetChannelInformation.ChannelInformation?> GetChannelInfo(string userId)
        {
            try
            {
                var channelInfo = await GetChannelInformation(userId);
                return channelInfo?.Data?.FirstOrDefault();
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing GetChannelInfo: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetChannelInfo(): {error}", error);
            }
            return null;
        }

        public async Task<TwitchLib.Api.Helix.Models.Games.Game?> GetGameInfo(string gameId)
        {
            try
            {
                var gameInfo = await _twitchApi.Helix.Games.GetGamesAsync([gameId]);
                return gameInfo?.Data?.FirstOrDefault();

            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing GetGameInfo: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetGameInfo(): {error}", error);
            }
            return null;
        }

        public async Task<string> GetStreamTitle()
        {
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting stream status.");
            try
            {
                var channelInfo = await GetChannelInformation(userId);
                if (channelInfo.Data.Length > 0)
                {
                    return channelInfo.Data[0].Title;
                }
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing Getting stream title: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetStreamTitle(): {error}", error);
            }
            return "";
        }

        public async Task<string> GetStreamThumbnail()
        {
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting stream status.");
            try
            {
                var streamInfo = await GetStreams(userId);
                if (streamInfo.Streams.Length > 0)
                {
                    var stream = streamInfo.Streams.First();
                    return stream.ThumbnailUrl;
                }
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing Getting Thumbnail: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetStreamThumbnail(): {error}", error);
            }
            return "";
        }

        public async Task RaidStreamer(string userId)
        {
            var broadcasterId = await GetBroadcasterUserId() ?? throw new Exception("Error getting stream status.");
            try
            {
                await _twitchApi.Helix.Raids.StartRaidAsync(broadcasterId, userId, _configuration["twitchAccessToken"]);
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing Raid: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing RaidStreamer(): {error}", error);
            }
        }

        public async Task<ShoutoutResponseEnum> ShoutoutStreamer(string userId)
        {
            var broadcasterId = await GetBroadcasterUserId() ?? throw new Exception("Error getting broadcaster id.");
            try
            {
                await _twitchApi.Helix.Chat.SendShoutoutAsync(broadcasterId, userId, broadcasterId, _configuration["twitchAccessToken"]);
                return ShoutoutResponseEnum.Success;
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing shoutout: {error}", error);
                if (ex.HttpResponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    return ShoutoutResponseEnum.TooManyRequests;
                }
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing ShoutoutStreamer(string): {error}", error);
            }
            return ShoutoutResponseEnum.Failure;
        }

        public async Task<List<TwitchLib.Api.Helix.Models.Clips.GetClips.Clip>> GetClips(string user)
        {
            var userId = await GetUserId(user);
            try
            {
                var result = await _twitchApi.Helix.Clips.GetClipsAsync(null, null, userId, null, null, null, null, null, 100, _configuration["twitchAccessToken"]);
                return [.. result.Clips];
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing GetClips: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetClips(): {error}", error);
            }
            return [];
        }

        public async Task<List<TwitchLib.Api.Helix.Models.Clips.GetClips.Clip>> GetFeaturedClips(string user)
        {
            var userId = await GetUserId(user);
            try
            {
                var result = await _twitchApi.Helix.Clips.GetClipsAsync(null, null, userId, null, null, null, null, true, 100, _configuration["twitchAccessToken"]);
                return [.. result.Clips];
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing GetFeaturedClips: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetFeaturedClips(): {error}", error);
            }
            return [];
        }

        public async Task<List<TwitchLib.Api.Helix.Models.Clips.GetClips.Clip>> GetClip(string clipId)
        {
            try
            {
                var result = await _twitchApi.Helix.Clips.GetClipsAsync([clipId], null, null, null, null, null, null, null, 1, _configuration["twitchAccessToken"]);
                return [.. result.Clips];
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing GetFeaturedClips: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetFeaturedClips(): {error}", error);
            }
            return [];
        }

        public async Task TimeoutUser(string name, string reason, int? length)
        {
            var broadcasterId = await GetBroadcasterUserId();
            if (broadcasterId == null)
            {
                _logger.LogError("Error getting broadcaster id.");
                return;
            }
            var userId = await GetUserId(name);
            if (userId == null)
            {
                _logger.LogError("Error getting user id.");
                return;
            }
            try
            {
                var banUserRequest = new TwitchLib.Api.Helix.Models.Moderation.BanUser.BanUserRequest
                {
                    UserId = userId,
                    Reason = reason,
                    Duration = length
                };
                await _twitchApi.Helix.Moderation.BanUserAsync(broadcasterId, broadcasterId, banUserRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error timing out user");
            }
        }

        public async Task Announcement(string message)
        {
            var broadcasterId = await GetBroadcasterUserId();
            if (broadcasterId == null)
            {
                _logger.LogError("Error getting broadcaster id.");
                return;
            }

            try
            {
                await _twitchApi.Helix.Chat.SendChatAnnouncementAsync(broadcasterId, broadcasterId, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error doing announcement");
            }

        }

        public async Task<List<ChannelEditor>> GetEditors()
        {
            var broadcasterId = await GetBroadcasterUserId();
            if (broadcasterId == null)
            {
                _logger.LogError("Error getting broadcaster id.");
                return [];
            }
            try
            {
                var editors = await _twitchApi.Helix.Channels.GetChannelEditorsAsync(broadcasterId, _configuration["twitchAccessToken"]);
                if (editors != null)
                {
                    return [.. editors.Data];
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting editors");
            }
            return [];
        }

        public async Task SubscribeToAllTheStuffs(string sessionId)
        {
            await ValidateAndRefreshToken();
            var userId = await GetBroadcasterUserId();
            if (userId == null) return;

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.chat.message", "1",
                new Dictionary<string, string>{
                    {"broadcaster_user_id", userId},
                    {"user_id", userId}
                },
                EventSubTransportMethod.Websocket,
                sessionId, accessToken: _configuration["twitchAccessToken"]
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.follow", "2",
                new Dictionary<string, string>{
                    {"broadcaster_user_id", userId},
                    {"moderator_user_id", userId}
                },
                EventSubTransportMethod.Websocket,
                sessionId, accessToken: _configuration["twitchAccessToken"]
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.subscribe", "1",
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                EventSubTransportMethod.Websocket,
                sessionId, accessToken: _configuration["twitchAccessToken"]
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.subscription.end", "1",
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                EventSubTransportMethod.Websocket,
                sessionId, accessToken: _configuration["twitchAccessToken"]
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.subscription.gift", "1",
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                EventSubTransportMethod.Websocket,
                sessionId, accessToken: _configuration["twitchAccessToken"]
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.subscription.message", "1",
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                EventSubTransportMethod.Websocket,
                sessionId, accessToken: _configuration["twitchAccessToken"]
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.cheer", "1",
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                EventSubTransportMethod.Websocket,
                sessionId, accessToken: _configuration["twitchAccessToken"]
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.channel_points_custom_reward_redemption.add",
                "1",
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                EventSubTransportMethod.Websocket,
                sessionId, accessToken: _configuration["twitchAccessToken"]
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "stream.offline",
                "1",
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                EventSubTransportMethod.Websocket,
                sessionId, accessToken: _configuration["twitchAccessToken"]
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "stream.online",
                "1",
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                EventSubTransportMethod.Websocket,
                sessionId, accessToken: _configuration["twitchAccessToken"]
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.raid",
                "1",
                new Dictionary<string, string>{{"to_broadcaster_user_id", userId},
                },
                EventSubTransportMethod.Websocket,
                sessionId, accessToken: _configuration["twitchAccessToken"]
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
               "channel.ban",
               "1",
               new Dictionary<string, string>{{"broadcaster_user_id", userId},
               },
               EventSubTransportMethod.Websocket,
               sessionId, accessToken: _configuration["twitchAccessToken"]
           );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
               "channel.unban",
               "1",
               new Dictionary<string, string>{{"broadcaster_user_id", userId},
               },
               EventSubTransportMethod.Websocket,
               sessionId, accessToken: _configuration["twitchAccessToken"]
           );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
               "channel.ad_break.begin",
               "1",
               new Dictionary<string, string>{{"broadcaster_user_id", userId},
               },
               EventSubTransportMethod.Websocket,
               sessionId, accessToken: _configuration["twitchAccessToken"]
           );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.suspicious_user.message",
                "1",
                new Dictionary<string, string>{
                    {"broadcaster_user_id", userId},
                    {"moderator_user_id", userId}
                },
                EventSubTransportMethod.Websocket,
               sessionId, accessToken: _configuration["twitchAccessToken"]
                );
            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.chat.message_delete",
                "1",
                new Dictionary<string, string>{
                    {"broadcaster_user_id", userId},
                    {"user_id", userId}
                },
                EventSubTransportMethod.Websocket,
               sessionId, accessToken: _configuration["twitchAccessToken"]
                );


        }

        public async Task ValidateAndRefreshToken()
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                var validToken = await _twitchApi.Auth.ValidateAccessTokenAsync(_configuration["twitchAccessToken"]);
                if (validToken != null && validToken.ExpiresIn > 1200)
                {
                    //TimeSpan.FromSeconds(validToken.ExpiresIn);
                   // await _settingsFileManager.AddOrUpdateAppSetting("expiresIn", validToken.ExpiresIn);
                    serviceUp = true;
                }
                else
                {
                    try
                    {
                        _logger.LogInformation("Refreshing Token");

                        var refreshToken = await _twitchApi.Auth.RefreshAuthTokenAsync(_configuration["twitchRefreshToken"], _configuration["twitchClientSecret"], _configuration["twitchClientId"]);
                        _configuration["twitchAccessToken"] = refreshToken.AccessToken;
                        _configuration["expiresIn"] = refreshToken.ExpiresIn.ToString();
                        _configuration["twitchRefreshToken"] = refreshToken.RefreshToken;
                        _twitchApi.Settings.AccessToken = refreshToken.AccessToken;
                        await _settingsFileManager.AddOrUpdateAppSetting("twitchAccessToken", refreshToken.AccessToken);
                        await _settingsFileManager.AddOrUpdateAppSetting("twitchRefreshToken", refreshToken.RefreshToken);
                        await _settingsFileManager.AddOrUpdateAppSetting("expiresIn", refreshToken.ExpiresIn.ToString());
                        serviceUp = true;
                    }
                    catch (Exception e)
                    {
                        _logger.LogError("Error refreshing token: {error}", e.Message);
                        serviceUp = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when validing/refreshing token");
                serviceUp = false;
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }


    }
}