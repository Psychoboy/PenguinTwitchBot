using PenguinTwitchBot.Bot.TwitchServices.TwitchModels;
using PenguinTwitchBot.TwitchApi.Auth;
using PenguinTwitchBot.TwitchApi.Helix;
using PenguinTwitchBot.TwitchApi.Models.ChannelPoints;
using PenguinTwitchBot.TwitchApi.Models.Channels;
using PenguinTwitchBot.TwitchApi.Models.Chat;
using PenguinTwitchBot.TwitchApi.Models.Clips;
using PenguinTwitchBot.TwitchApi.Models.EventSub;
using PenguinTwitchBot.TwitchApi.Models.Games;
using PenguinTwitchBot.TwitchApi.Models.Moderation;
using PenguinTwitchBot.TwitchApi.Models.Schedule;
using PenguinTwitchBot.TwitchApi.Models.Subscriptions;
using PenguinTwitchBot.TwitchApi.Models.Users;
using System.Collections.Concurrent;
using System.Timers;
using Timer = System.Timers.Timer;

namespace PenguinTwitchBot.Bot.TwitchServices
{
    /// <summary>
    /// Everything here is executed as the streamer
    /// </summary>
    public class TwitchService : ITwitchService
    {
        private readonly ILogger<TwitchService> _logger;
        private readonly IConfiguration _configuration;
        static readonly SemaphoreSlim semaphoreSlim = new(1, 1);
        readonly ConcurrentDictionary<string, User?> UserCache = new();
        readonly Timer _timer;
        private readonly IAuthClient _authClient;
        private readonly IChatClient _chatClient;
        private readonly IChannelPointsClient _channelPointsClient;
        private readonly IModerationEventSubClient _moderationEventSubClient;
        private readonly IChannelsClient _channelsClient;
        private readonly IStreamsClient _streamsClient;
        private readonly IClipsClient _clipsClient;
        private readonly IGamesClient _gamesClient;
        private readonly ISubscriptionsClient _subscriptionsClient;
        private readonly IRaidsClient _raidsClient;
        private readonly IUsersClient _usersClient;
        private readonly IScheduleClient _scheduleClient;
        private readonly SettingsFileManager _settingsFileManager;
        private readonly ChatMessageIdTracker _messageIdTracker;
        private readonly Application.Notifications.IPenguinDispatcher _dispatcher;
        private bool serviceUp = false;
        private string? broadcasterId = string.Empty;
        private string? botId = string.Empty;
        private bool lastRefreshFailed = false;
        private volatile string _accessToken = string.Empty;

        public TwitchService(
            ILogger<TwitchService> logger,
            IConfiguration configuration,
            IAuthClient authClient,
            IChatClient chatClient,
            IChannelPointsClient channelPointsClient,
            IModerationEventSubClient moderationEventSubClient,
            IChannelsClient channelsClient,
            IStreamsClient streamsClient,
            IClipsClient clipsClient,
            IGamesClient gamesClient,
            ISubscriptionsClient subscriptionsClient,
            IRaidsClient raidsClient,
            IUsersClient usersClient,
            IScheduleClient scheduleClient,
            SettingsFileManager settingsFileManager,
            Application.Notifications.IPenguinDispatcher dispatcher,
            ChatMessageIdTracker messageIdTracker)
        {

            _logger = logger;
            _settingsFileManager = settingsFileManager;
            _configuration = configuration;
            _authClient = authClient;
            _chatClient = chatClient;
            _channelPointsClient = channelPointsClient;
            _moderationEventSubClient = moderationEventSubClient;
            _channelsClient = channelsClient;
            _streamsClient = streamsClient;
            _clipsClient = clipsClient;
            _gamesClient = gamesClient;
            _subscriptionsClient = subscriptionsClient;
            _raidsClient = raidsClient;
            _usersClient = usersClient;
            _scheduleClient = scheduleClient;
            _timer = new Timer();
            _timer = new Timer(300000); //5 minutes
            _timer.Elapsed += OnTimerElapsed;
            _timer.Start();
            _messageIdTracker = messageIdTracker;
            _dispatcher = dispatcher;
            _accessToken = _configuration["twitchAccessToken"] ?? string.Empty;
        }

        public void SetAccessToken(string accessToken)
        {
            _accessToken = accessToken;
            _configuration["twitchAccessToken"] = accessToken;
        }

        public bool IsServiceUp()
        {
            return serviceUp;
        }

        public async Task SendMesssageAsStreamer(string message)
        {
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                //var result = await _twitchApi.Helix.Chat.SendChatMessage(broadcasterId, broadcasterId, message);
                var msg = new SendChatMessageRequest
                {
                    BroadcasterId = broadcasterId,
                    SenderId = broadcasterId,
                    Message = message
                };
                var result = await _chatClient.SendChatMessageAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    msg);
                if (result.Data.First().IsSent == false)
                {
                    _logger.LogWarning("Message failed to send: {reason}", result.Data.First().DropReason.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message.");
            }
        }

        public async Task SendMessage(string message)
        {
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                //var result = await _twitchApi.Helix.Chat.SendChatMessage(broadcasterId, broadcasterId, message);
                var msg = new SendChatMessageRequest
                {
                    BroadcasterId = broadcasterId,
                    SenderId = broadcasterId,
                    Message = message
                };
                var result = await _chatClient.SendChatMessageAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    msg);
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

        public async Task<ChannelStreamSchedule?> GetStreamSchedule()
        {
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                var result = await _scheduleClient.GetChannelStreamScheduleAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId!);
                if (result?.Schedule != null)
                {
                    return ScheduleClient.MapToChannelStreamSchedule(result.Schedule);
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
            if(!await ValidateAndRefreshToken()) _logger.LogError("Failed to refresh token");
    
        }

        public async Task<IEnumerable<Chatter>> GetCurrentChatters()
        {
            if(!await ValidateAndRefreshToken())
            {
                _logger.LogError("Failed to refresh token");
                throw new InvalidOperationException("Failed to refresh token");
            }
            var broadcasterId = await GetBroadcasterUserId();
            if (string.IsNullOrWhiteSpace(broadcasterId))
            {
                _logger.LogError("Error getting broadcaster id.");
                return [];
            }
            var after = "";
            List<Chatter> chatters = [];
            try
            {
                while (true)
                {
                    var curChatters = await _chatClient.GetChattersAsync(
                        _configuration["twitchClientId"]!,
                        _accessToken,
                        broadcasterId,
                        broadcasterId,
                        after);
                    if (curChatters != null)
                    {
                        chatters.AddRange(curChatters.Data.Select(ChatClient.MapToChatter));
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

        public async Task<ChannelPointReward?> GetCustomReward(string id)
        {
            if(!await ValidateAndRefreshToken())
            {
                _logger.LogError("Failed to refresh token");
                return null;
            }
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                if (string.IsNullOrWhiteSpace(broadcasterId))
                {
                    _logger.LogError("Error getting broadcaster id.");
                    return null;
                }

                var result = await _channelPointsClient.GetCustomRewardAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    [id]);
                return result == null
                    ? throw new InvalidOperationException("Result from Twitch API getting channel point reward was null")
                    : ChannelPointsClient.MapToChannelPointReward(result.Data.First());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting custom reward.");
            }
            return null;
        }

        public async Task<bool> WillBePermittedByAutomod(string message)
        {
            if(!await ValidateAndRefreshToken())
            {
                _logger.LogError("Failed to refresh token");
                return false;
            }
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                if (string.IsNullOrWhiteSpace(broadcasterId))
                {
                    _logger.LogError("Error getting broadcaster id.");
                    return false;
                }

                var result = await _moderationEventSubClient.CheckAutoModStatusAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    new()
                {
                    new() {
                        MsgId = Guid.NewGuid().ToString(),
                        MsgText = message
                    }
                }, broadcasterId);
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

        public async Task CreateChannelPointReward(CreateChannelPointRewardRequest request)
        {
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                if (string.IsNullOrWhiteSpace(broadcasterId))
                {
                    _logger.LogError("Error getting broadcaster id.");
                    return;
                }

                await _channelPointsClient.CreateCustomRewardsAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating channel point reward");
                throw;
            }
        }

        public async Task<IEnumerable<ChannelPointReward>> GetChannelPointRewards()
        {
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                if (string.IsNullOrWhiteSpace(broadcasterId))
                {
                    _logger.LogError("Error getting broadcaster id.");
                    return [];
                }

                var rewards = await _channelPointsClient.GetCustomRewardAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId);
                if (rewards != null)
                {
                    return rewards.Data.Select(ChannelPointsClient.MapToChannelPointReward).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting channel point rewards.");
            }
            return [];
        }

        public async Task<IEnumerable<ChannelPointReward>> GetChannelPointRewards(bool onlyManageable)
        {
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                if (string.IsNullOrWhiteSpace(broadcasterId))
                {
                    _logger.LogError("Error getting broadcaster id.");
                    return [];
                }

                var rewards = await _channelPointsClient.GetCustomRewardAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    onlyManageableRewards: onlyManageable);
                if (rewards != null)
                {
                    return rewards.Data.Select(ChannelPointsClient.MapToChannelPointReward).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting channel point rewards.");
            }
            return [];
        }

        public async Task UpdateChannelPointReward(string rewardId, UpdateCustomRewardRequest request)
        {
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                if (string.IsNullOrWhiteSpace(broadcasterId))
                {
                    _logger.LogError("Error getting broadcaster id.");
                    return;
                }

                await _channelPointsClient.UpdateCustomRewardAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    rewardId,
                    request);
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
                if (string.IsNullOrWhiteSpace(broadcasterId))
                {
                    _logger.LogError("Error getting broadcaster id.");
                    return;
                }

                await _channelPointsClient.DeleteCustomRewardAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    rewardId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting channel point rewards.");
            }
        }

        public async Task<List<Subscription>> GetAllSubscriptions()
        {
            if(!await ValidateAndRefreshToken())
            {
                _logger.LogError("Failed to refresh token");
                throw new InvalidOperationException("Failed to refresh token");
            }
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting user id.");
            List<Subscription> subs = [];
            var after = "";
            while (true)
            {
                var curSubs = await _subscriptionsClient.GetBroadcasterSubscriptionsAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    userId,
                    100,
                    after);
                if (curSubs != null)
                {
                    subs.AddRange(curSubs.Data.Select(SubscriptionsClient.MapToSubscription));
                    if (string.IsNullOrEmpty(curSubs.Pagination.Cursor))
                    {
                        break;
                    }
                    after = curSubs.Pagination.Cursor;
                }
            }
            return subs;
        }

        public async Task<List<BannedUser>> GetAllBannedViewers()
        {
            if(!await ValidateAndRefreshToken())
            {
                _logger.LogError("Failed to refresh token");
                throw new InvalidOperationException("Failed to refresh token");
            }    
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting user id.");
            var after = "";
            List<BannedUser> curBannedUsers = [];
            while (true)
            {
                var bannedUsers = await _moderationEventSubClient.GetBannedUsersAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    userId,
                    after);
                if (bannedUsers != null)
                {
                    curBannedUsers.AddRange(bannedUsers.Data.Select(ModerationEventSubClient.MapToBannedUser));
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

        public async Task<User?> GetUserByName(string user)
        {
            try
            {
                if (UserCache.TryGetValue(user, out var cachedUser))
                {
                    return cachedUser;
                }

                var users = await _usersClient.GetUsersAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    null,
                    [user]);
                var userObj = users.Users.FirstOrDefault();
                if (userObj != null)
                {
                    var mappedUser = UsersClient.MapToUser(userObj);
                    UserCache[user] = mappedUser;
                    return mappedUser;
                }
                UserCache[user] = null;
                return null;
            }
            catch (Exception ex) when (ex.GetType().Name == "TooManyRequestsException")
            {
                return null;
            }
            catch (Exception)
            {
                var safeUser = (user ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
                _logger.LogCritical("GetUserByName - Failed getting user: {user}", safeUser);
                return null;
            }
        }

        public async Task<User?> GetUserById(string userId)
        {
            try
            {
                var users = await _usersClient.GetUsersAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    [userId],
                    null);
                var userObj = users.Users.FirstOrDefault();
                return userObj != null ? UsersClient.MapToUser(userObj) : null;
            }
            catch (Exception ex) when (ex.GetType().Name == "TooManyRequestsException")
            {
                return null;
            }
            catch (Exception)
            {
                _logger.LogCritical("GetUserById - Failed getting user: {user}", userId);
                return null;
            }
        }

        public async Task<List<User?>?> GetUsers(List<string> userNames)
        {
            try
            {
                var users = await _usersClient.GetUsersAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    null,
                    userNames);
                if (users == null) throw new Exception("Got a null response");
                var mappedUsers = users.Users.Select(u => (User?)UsersClient.MapToUser(u)).ToList();
                foreach (var user in users.Users)
                {
                    UserCache[user.Login] = UsersClient.MapToUser(user);
                }
                return mappedUsers;
            }
            catch (Exception ex) when (ex.GetType().Name == "TooManyRequestsException")
            {
                return null;
            }
            catch (Exception)
            {
                _logger.LogCritical("Failed getting users: {users}", string.Join(", ", userNames));
                return null;
            }
        }

        public async Task<Follower?> GetUserFollow(string user)
        {
            var broadcasterId = await GetBroadcasterUserId();
            var userId = await GetUserId(user);
            if (userId == null) return null;
            if (broadcasterId == null) return null;
            if (broadcasterId == userId)
            {
                return new()
                {
                    UserId = userId,
                    DisplayName = user,
                    Username = user,
                    FollowDate = DateTime.UtcNow
                };

            }
            try
            {
                //var response = await _twitchApi.Helix.Users.GetUsersFollowsAsync(null, null, 1, userId, broadcasterId, _accessToken);
                var response = await _channelsClient.GetChannelFollowersAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    userId,
                    1,
                    null);
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
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
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
                var response = await _subscriptionsClient.CheckUserSubscriptionAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId!,
                    userId);
                if (response == null) return false;
                return response.Data.Length != 0;
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
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
            if (userId == null || string.IsNullOrWhiteSpace(broadcasterId)) return false;
            try
            {
                var response = await _moderationEventSubClient.GetModeratorsAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    [userId]);
                return response.Data.Length != 0;
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing IsUserMod(): {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing IsUserMod(): {error}", error);
            }
            return false;
        }

        private sealed record StreamSnapshot(
            string UserId,
            string UserName,
            string GameName,
            int ViewerCount,
            DateTime StartedAt,
            string ThumbnailUrl);

        private async Task<List<StreamSnapshot>> GetStreams(List<string> userIds)
        {
            var response = await _streamsClient.GetStreamsAsync(
                _configuration["twitchClientId"]!,
                _accessToken,
                userIds: userIds);

            if (response?.Streams == null)
            {
                return [];
            }

            return response.Streams
                .Select(s => new StreamSnapshot(s.UserId, s.UserName, s.GameName, s.ViewerCount, s.StartedAt, s.ThumbnailUrl))
                .ToList();
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
                var streams = await GetStreams([userId]);
                return streams.Count > 0;
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
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
                return streams.Select(x => new TwitchModels.OnlineStream
                {
                    UserId = x.UserId,
                    UserName = x.UserName,
                    DisplayName = x.UserName,
                    Game = x.GameName
                }).ToList();
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
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
                var streams = await GetStreams([userId]);
                if (streams.Count == 0)
                {
                    return DateTime.MinValue;
                }
                var stream = streams.First();
                var startTime = stream.StartedAt;
                return startTime;
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
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
                var streams = await GetStreams([userId]);
                if (streams.Count == 0)
                {
                    return 0;
                }
                return streams.First().ViewerCount;
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
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

        private async Task<List<ChannelInformation>> GetChannelInformation(string userId)
        {
            var response = await _channelsClient.GetChannelInformationAsync(
                _configuration["twitchClientId"]!,
                _accessToken,
                userId);

            if (response?.Data == null)
            {
                return [];
            }

            return response.Data.Select(ChannelsClient.MapToChannelInformation).ToList();
        }

        public async Task<string> GetCurrentGame(string userId)
        {
            try
            {
                var channelInfo = await GetChannelInformation(userId);
                if (channelInfo.Count > 0)
                {
                    return channelInfo[0].GameName;
                }
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing Getting current game: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetCurrentGame(): {error}", error);
            }
            return "";
        }

        public async Task<ChannelInformation?> GetChannelInfo(string userId)
        {
            try
            {
                var channelInfo = await GetChannelInformation(userId);
                return channelInfo.FirstOrDefault();
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetChannelInfo: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetChannelInfo(): {error}", error);
            }
            return null;
        }

        public async Task<string> GetUserBio(string userId)
        {
            try
            {
                var user = await GetUserById(userId);
                if (user != null)
                {
                    return user.Description;
                }
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing Getting user bio: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetUserBio(): {error}", error);
            }
            return "";
        }

        public async Task<string> GetUserStreamTitle(string userId)
        {
            try
            {
                var channelInfo = await GetChannelInformation(userId);
                if (channelInfo.Count > 0)
                {
                    return channelInfo[0].Title;
                }
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing Getting user stream title: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetUserStreamTitle(): {error}", error);
            }
            return "";
        }

        public async Task<Game?> GetGameInfo(string gameId)
        {
            try
            {
                var gameInfo = await _gamesClient.GetGamesAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    [gameId]);
                var game = gameInfo?.Data?.FirstOrDefault();
                return game != null ? GamesClient.MapToGame(game) : null;
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
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
                if (channelInfo.Count > 0)
                {
                    return channelInfo[0].Title;
                }
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
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
                var streamInfo = await GetStreams([userId]);
                if (streamInfo.Count > 0)
                {
                    var stream = streamInfo.First();
                    return stream.ThumbnailUrl;
                }
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
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
                await _raidsClient.StartRaidAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    userId);
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
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
                await _chatClient.SendShoutoutAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    userId,
                    broadcasterId);
                return ShoutoutResponseEnum.Success;
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing shoutout: {error}", error);
                if (error.Contains("429", StringComparison.OrdinalIgnoreCase) || error.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase))
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

        public async Task<List<Clip>> GetClips(string user)
        {
            var userId = await GetUserId(user);
            try
            {
                var result = await _clipsClient.GetClipsAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    null,
                    userId,
                    100,
                    null);
                return result.Clips.Select(c => ClipsClient.MapToClip(c)).ToList();
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetClips: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetClips(): {error}", error);
            }
            return [];
        }

        public async Task<List<Clip>> GetFeaturedClips(string user)
        {
            var userId = await GetUserId(user);
            try
            {
                var result = await _clipsClient.GetClipsAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    null,
                    userId,
                    100,
                    true);
                return result.Clips.Select(c => ClipsClient.MapToClip(c)).ToList();
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetFeaturedClips: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetFeaturedClips(): {error}", error);
            }
            return [];
        }

        public async Task<List<Clip>> GetClip(string clipId)
        {
            try
            {
                var result = await _clipsClient.GetClipsByIdAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    [clipId]);
                return result.Clips.Select(c => ClipsClient.MapToClip(c)).ToList();
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetClip: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetClip(): {error}", error);
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
                await _moderationEventSubClient.BanUserAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    broadcasterId,
                    new()
                    {
                        UserId = userId,
                        Reason = reason,
                        Duration = length
                    });
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
                await _chatClient.SendChatAnnouncementAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    broadcasterId,
                    message);
                _logger.LogInformation("Announcement sent: {message}", message);
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
                var editors = await _channelsClient.GetChannelEditorsAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId);
                if (editors != null)
                {
                    return editors.Data.Select(e => ChannelsClient.MapToChannelEditor(e)).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting editors");
            }
            return [];
        }

        public async Task DeleteMessage(string messageId)
        {
            var broadcasterId = await GetBroadcasterUserId();
            if (broadcasterId == null)
            {
                _logger.LogError("Error getting broadcaster id.");
                return;
            }
            try
            {
                await _moderationEventSubClient.DeleteChatMessagesAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    broadcasterId,
                    messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message");
            }
        }

        public async Task<bool> SubscribeToAllTheStuffs(string sessionId)
        {
            if(!await ValidateAndRefreshToken())
            {
                _logger.LogError("Failed to refresh token");
                throw new InvalidOperationException("Failed to refresh token");
            }
            var userId = await GetBroadcasterUserId();
            if (userId == null) {
                _logger.LogError("Error getting broadcaster id.");
                throw new Exception("Error getting broadcaster id.");
            }

            var response = await CreateWebsocketEventSubscription(
                "channel.chat.message",
                "1",
                new Dictionary<string, string>
                {
                    { "broadcaster_user_id", userId },
                    { "user_id", userId }
                },
                sessionId);
            ValidateEventSubscription(response, "channel.chat.message");

            response = await CreateWebsocketEventSubscription(
                "channel.follow",
                "2",
                new Dictionary<string, string>
                {
                    { "broadcaster_user_id", userId },
                    { "moderator_user_id", userId }
                },
                sessionId);
            ValidateEventSubscription(response, "channel.follow");

            response = await CreateWebsocketEventSubscription("channel.subscribe", "1", new() { { "broadcaster_user_id", userId } }, sessionId);
            ValidateEventSubscription(response, "channel.subscribe");

            response = await CreateWebsocketEventSubscription("channel.subscription.end", "1", new() { { "broadcaster_user_id", userId } }, sessionId);
            ValidateEventSubscription(response, "channel.subscription.end");

            response = await CreateWebsocketEventSubscription("channel.subscription.gift", "1", new() { { "broadcaster_user_id", userId } }, sessionId);
            ValidateEventSubscription(response, "channel.subscription.gift");

            response = await CreateWebsocketEventSubscription("channel.subscription.message", "1", new() { { "broadcaster_user_id", userId } }, sessionId);
            ValidateEventSubscription(response, "channel.subscription.message");

            response = await CreateWebsocketEventSubscription("channel.cheer", "1", new() { { "broadcaster_user_id", userId } }, sessionId);
            ValidateEventSubscription(response, "channel.cheer");

            response = await CreateWebsocketEventSubscription("channel.bits.use", "1", new() { { "broadcaster_user_id", userId } }, sessionId);
            ValidateEventSubscription(response, "channel.bits.use");

            response = await CreateWebsocketEventSubscription("channel.channel_points_custom_reward_redemption.add", "1", new() { { "broadcaster_user_id", userId } }, sessionId);
            ValidateEventSubscription(response, "channel.channel_points_custom_reward_redemption.add");

            response = await CreateWebsocketEventSubscription("stream.offline", "1", new() { { "broadcaster_user_id", userId } }, sessionId);
            ValidateEventSubscription(response, "stream.offline");

            response = await CreateWebsocketEventSubscription("stream.online", "1", new() { { "broadcaster_user_id", userId } }, sessionId);
            ValidateEventSubscription(response, "stream.online");

            response = await CreateWebsocketEventSubscription("channel.raid", "1", new() { { "to_broadcaster_user_id", userId } }, sessionId);
            ValidateEventSubscription(response, "channel.raid");

            response = await CreateWebsocketEventSubscription("channel.ban", "1", new() { { "broadcaster_user_id", userId } }, sessionId);
            ValidateEventSubscription(response, "channel.ban");

            response = await CreateWebsocketEventSubscription("channel.unban", "1", new() { { "broadcaster_user_id", userId } }, sessionId);
            ValidateEventSubscription(response, "channel.unban");

            response = await CreateWebsocketEventSubscription("channel.ad_break.begin", "1", new() { { "broadcaster_user_id", userId } }, sessionId);
            ValidateEventSubscription(response, "channel.ad_break.begin");

            response = await CreateWebsocketEventSubscription(
                "channel.suspicious_user.message",
                "1",
                new()
                {
                    { "broadcaster_user_id", userId },
                    { "moderator_user_id", userId }
                },
                sessionId);
            ValidateEventSubscription(response, "channel.suspicious_user.message");

            response = await CreateWebsocketEventSubscription(
                "channel.chat.message_delete",
                "1",
                new()
                {
                    { "broadcaster_user_id", userId },
                    { "user_id", userId }
                },
                sessionId);
            ValidateEventSubscription(response, "channel.chat.message_delete");

            response = await CreateWebsocketEventSubscription(
                "channel.chat.notification",
                "1",
                new()
                {
                    { "broadcaster_user_id", userId },
                    { "user_id", userId }
                },
                sessionId);
            ValidateEventSubscription(response, "channel.chat.notification");

            return true;
        }

        private async Task<EventSubSubscriptionResult> CreateWebsocketEventSubscription(
            string type,
            string version,
            Dictionary<string, string> condition,
            string sessionId)
        {
            return await _moderationEventSubClient.CreateEventSubSubscriptionAsync(
                _configuration["twitchClientId"]!,
                _accessToken,
                type,
                version,
                condition,
                EventSubTransportMethod.Websocket,
                sessionId);
        }

        private void ValidateEventSubscription(EventSubSubscriptionResult result, string eventName)
        {
            if (!result.IsEnabled)
            {
                _logger.LogError("Event subscription for {eventName} is not enabled", eventName);
                throw new Exception("Event subscription is not enabled");
            }
        }

        public async Task<bool> ValidateAndRefreshToken()
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                var validToken = await _authClient.ValidateAccessTokenAsync(_accessToken);
                if (validToken != null && validToken.ExpiresIn > 1200)
                {
                    serviceUp = true;
                }
                else
                {
                    try
                    {
                        _logger.LogInformation("Refreshing Token");

                        var refreshToken = await _authClient.RefreshAuthTokenAsync(_configuration["twitchRefreshToken"]!, _configuration["twitchClientSecret"]!, _configuration["twitchClientId"]!);
                        _accessToken = refreshToken.AccessToken;
                        _configuration["expiresIn"] = refreshToken.ExpiresIn.ToString();
                        _configuration["twitchRefreshToken"] = refreshToken.RefreshToken;
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

            if (serviceUp && lastRefreshFailed)
            {
                _logger.LogInformation("Twitch service is up");
                lastRefreshFailed = false;
                await _dispatcher.Publish(new ServiceRestored());
            }
            else if (!serviceUp && !lastRefreshFailed)
            {
                lastRefreshFailed = true;
            }

            return serviceUp;
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, string>> GetChatBadgesAsync()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                var globalBadges = await _chatClient.GetGlobalChatBadgesAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken);
                if (globalBadges?.EmoteSet != null)
                {
                    foreach (var set in globalBadges.EmoteSet)
                    {
                        foreach (var version in set.Versions ?? [])
                        {
                            result[$"{set.SetId}/{version.Id}"] = version.ImageUrl1x;
                        }
                    }
                }

                var broadcasterId = await GetBroadcasterUserId();
                if (!string.IsNullOrEmpty(broadcasterId))
                {
                    var channelBadges = await _chatClient.GetChannelChatBadgesAsync(
                        _configuration["twitchClientId"]!,
                        _accessToken,
                        broadcasterId);
                    if (channelBadges?.EmoteSet != null)
                    {
                        foreach (var set in channelBadges.EmoteSet)
                        {
                            foreach (var version in set.Versions ?? [])
                            {
                                // Channel badges override globals for the same key
                                result[$"{set.SetId}/{version.Id}"] = version.ImageUrl1x;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch chat badges");
            }
            return result;
        }

    }
}
