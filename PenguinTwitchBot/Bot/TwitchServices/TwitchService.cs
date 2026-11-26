using PenguinTwitchBot.Bot.TwitchServices.TwitchModels;
using PenguinTwitchBot.TwitchApi.Auth;
using PenguinTwitchBot.TwitchApi.Helix;
using PenguinTwitchBot.TwitchApi.Models.EventSub;
using PenguinTwitchBot.TwitchApi.Models.Users;
using System.Collections.Concurrent;
using System.Timers;
using Timer = System.Timers.Timer;

namespace PenguinTwitchBot.Bot.TwitchServices
{
    /// <summary>
    /// Everything here is executed as the streamer
    /// </summary>
    public partial class TwitchService : ITwitchService
    {
        private readonly ILogger<TwitchService> _logger;
        private readonly IConfiguration _configuration;
        static readonly SemaphoreSlim semaphoreSlim = new(1, 1);
        private static readonly TimeSpan UserCacheTtl = TimeSpan.FromHours(6);
        private sealed record CachedUserEntry(User? User, DateTime CachedAtUtc);
        readonly ConcurrentDictionary<string, CachedUserEntry> UserCache = new(StringComparer.OrdinalIgnoreCase);
        readonly ConcurrentDictionary<string, CachedUserEntry> UserByIdCache = new(StringComparer.OrdinalIgnoreCase);
        readonly Timer _timer;
        private readonly IAuthClient _authClient;
        private readonly IChatClient _chatClient;
        private readonly IChannelPointsClient _channelPointsClient;
        private readonly IModerationClient _moderationClient;
        private readonly IChannelsClient _channelsClient;
        private readonly IStreamsClient _streamsClient;
        private readonly IClipsClient _clipsClient;
        private readonly IGamesClient _gamesClient;
        private readonly ISubscriptionsClient _subscriptionsClient;
        private readonly IRaidsClient _raidsClient;
        private readonly IUsersClient _usersClient;
        private readonly IScheduleClient _scheduleClient;
        private readonly ITeamsClient _teamsClient;
        private readonly SettingsFileManager _settingsFileManager;
        private readonly IChatMessageIdTracker _messageIdTracker;
        private readonly Application.Notifications.IPenguinDispatcher _dispatcher;
        private bool serviceUp = false;
        private TwitchServiceStatus _status = TwitchServiceStatus.Unknown;
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
            IModerationClient moderationClient,
            IChannelsClient channelsClient,
            IStreamsClient streamsClient,
            IClipsClient clipsClient,
            IGamesClient gamesClient,
            ISubscriptionsClient subscriptionsClient,
            IRaidsClient raidsClient,
            IUsersClient usersClient,
            IScheduleClient scheduleClient,
            ITeamsClient teamsClient,
            SettingsFileManager settingsFileManager,
            Application.Notifications.IPenguinDispatcher dispatcher,
            IChatMessageIdTracker messageIdTracker)
        {

            _logger = logger;
            _settingsFileManager = settingsFileManager;
            _configuration = configuration;
            _authClient = authClient;
            _chatClient = chatClient;
            _channelPointsClient = channelPointsClient;
            _moderationClient = moderationClient;
            _channelsClient = channelsClient;
            _streamsClient = streamsClient;
            _clipsClient = clipsClient;
            _gamesClient = gamesClient;
            _subscriptionsClient = subscriptionsClient;
            _raidsClient = raidsClient;
            _usersClient = usersClient;
            _scheduleClient = scheduleClient;
            _teamsClient = teamsClient;
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

        public TwitchServiceStatus GetStatus()
        {
            return _status;
        }

        public event EventHandler<TwitchServiceStatus>? ServiceStatusChanged;

        private void PruneExpiredUserCacheEntries()
        {
            var cutoffUtc = DateTime.UtcNow - UserCacheTtl;
            foreach (var kvp in UserCache)
            {
                if (kvp.Value.CachedAtUtc < cutoffUtc)
                {
                    UserCache.TryRemove(kvp);
                }
            }
            foreach (var kvp in UserByIdCache)
            {
                if (kvp.Value.CachedAtUtc < cutoffUtc)
                {
                    UserByIdCache.TryRemove(kvp);
                }
            }
        }

        private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                PruneExpiredUserCacheEntries();
                if (!await ValidateAndRefreshToken()) _logger.LogError("Failed to refresh token");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during TwitchService token refresh timer");
            }
        }

        public async Task<bool> SubscribeToAllTheStuffs(string sessionId)
        {
            // return true;
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

            // Outgoing raids (we raid someone else). Needed for the Raid Reward feature.
            response = await CreateWebsocketEventSubscription("channel.raid", "1", new() { { "from_broadcaster_user_id", userId } }, sessionId);
            ValidateEventSubscription(response, "channel.raid (outgoing)");

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
            return await _moderationClient.CreateEventSubSubscriptionAsync(
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
            var previousServiceUp = serviceUp;
            var previousStatus = _status;
            TwitchServiceStatus capturedStatus;
            bool capturedServiceUp;
            bool shouldPublishRestored = false;

            try
            {
                if (string.IsNullOrWhiteSpace(_accessToken))
                {
                    serviceUp = false;
                    _status = TwitchServiceStatus.AuthenticationDisconnected;
                }
                else
                {
                    try
                    {
                        var validToken = await _authClient.ValidateAccessTokenAsync(_accessToken);
                        if (validToken != null && validToken.ExpiresIn > 1200)
                        {
                            serviceUp = true;
                            _status = TwitchServiceStatus.Connected;
                        }
                        else
                        {
                            try
                            {
                                serviceUp = await RefreshToken();
                                _status = serviceUp ? TwitchServiceStatus.Connected : TwitchServiceStatus.AuthenticationDisconnected;
                            }
                            catch (Exception e)
                            {
                                _logger.LogError("Error refreshing token: {error}", e.Message);
                                serviceUp = false;
                                _status = TwitchServiceStatus.AuthenticationDisconnected;
                            }
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            _logger.LogWarning("Unauthorized when validating token, attempting refresh");
                            try
                            {
                                serviceUp = await RefreshToken();
                                _status = serviceUp ? TwitchServiceStatus.Connected : TwitchServiceStatus.AuthenticationDisconnected;
                            }
                            catch (Exception e)
                            {
                                _logger.LogError("Error refreshing token: {error}", e.Message);
                                serviceUp = false;
                                _status = TwitchServiceStatus.AuthenticationDisconnected;
                            }
                        }
                        else
                        {
                            _logger.LogError(ex, "HTTP error when validating/refreshing token");
                            serviceUp = false;
                            _status = TwitchServiceStatus.Unavailable;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error when validing/refreshing token");
                        serviceUp = false;
                        _status = TwitchServiceStatus.Unavailable;
                    }
                }

                if (serviceUp && lastRefreshFailed)
                {
                    _logger.LogInformation("Twitch service is up");
                    lastRefreshFailed = false;
                    shouldPublishRestored = true;
                }
                else if (!serviceUp && !lastRefreshFailed)
                {
                    lastRefreshFailed = true;
                }

                capturedStatus = _status;
                capturedServiceUp = serviceUp;
            }
            finally
            {
                semaphoreSlim.Release();
            }

            if (shouldPublishRestored)
            {
                await _dispatcher.Publish(new ServiceRestored());
            }

            if (capturedStatus != previousStatus)
            {
                ServiceStatusChanged?.Invoke(this, capturedStatus);
            }

            return capturedServiceUp;
        }

        public async Task<bool> RefreshToken()
        {
            try
            {
                _logger.LogInformation("Refreshing Token");

                var refreshToken = await _authClient.RefreshAuthTokenAsync(_configuration["twitchRefreshToken"]!, _configuration["twitchClientSecret"]!, _configuration["twitchClientId"]!);
                _accessToken = refreshToken.AccessToken;
                _configuration["twitchAccessToken"] = refreshToken.AccessToken;
                _configuration["twitchRefreshToken"] = refreshToken.RefreshToken;
                await _settingsFileManager.AddOrUpdateAppSetting("twitchAccessToken", refreshToken.AccessToken);
                await _settingsFileManager.AddOrUpdateAppSetting("twitchRefreshToken", refreshToken.RefreshToken);
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError("Error refreshing token: {error}", e.Message);
                return false;
            }
        }
    }
}
