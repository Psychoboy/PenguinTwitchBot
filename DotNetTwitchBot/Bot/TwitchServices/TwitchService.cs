using System.Collections.Concurrent;
using System.Reflection.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Models;
using Newtonsoft.Json;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.Subscriptions;
using System.Timers;
using Timer = System.Timers.Timer;
using TwitchLib.Api.Core.Exceptions;

namespace DotNetTwitchBot.Bot.TwitchServices
{
    public class TwitchService
    {
        private readonly TwitchAPI _twitchApi = new();
        private readonly ILogger<TwitchService> _logger;
        private readonly IConfiguration _configuration;
        static readonly SemaphoreSlim semaphoreSlim = new(1, 1);
        readonly ConcurrentDictionary<string, TwitchLib.Api.Helix.Models.Users.GetUsers.User?> UserCache = new();
        readonly Timer _timer;
        private readonly SettingsFileManager _settingsFileManager;

        public TwitchService(ILogger<TwitchService> logger, IConfiguration configuration, SettingsFileManager settingsFileManager)
        {

            _logger = logger;
            _settingsFileManager = settingsFileManager;
            _configuration = configuration;
            _twitchApi.Settings.ClientId = _configuration["twitchClientId"];
            _twitchApi.Settings.AccessToken = _configuration["twitchAccessToken"];
            _twitchApi.Settings.Scopes = new List<AuthScopes>();
            _timer = new Timer();
            _timer = new Timer(300000); //5 minutes
            _timer.Elapsed += OnTimerElapsed;
            _timer.Start();


            foreach (var authScope in Enum.GetValues(typeof(AuthScopes)))
            {
                if ((AuthScopes)authScope == AuthScopes.Any) continue;
                _twitchApi.Settings.Scopes.Add((AuthScopes)authScope);
            }

        }

        private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            await ValidateAndRefreshToken();
        }

        public async Task<List<Subscription>> GetAllSubscriptions()
        {
            await ValidateAndRefreshToken();
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting user id.");
            List<Subscription> subs = new();
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

        public async Task<string?> GetBroadcasterUserId()
        {
            var broadcaster = _configuration["broadcaster"];
            if (broadcaster == null) return null;
            return await GetUserId(broadcaster);
        }

        public async Task<string?> GetBotUserId()
        {
            var bot = _configuration["botName"];
            if (bot == null) return null;
            return await GetUserId(bot);
        }

        public async Task<string?> GetUserId(string user)
        {
            return (await GetUser(user))?.Id;
        }

        public async Task<TwitchLib.Api.Helix.Models.Users.GetUsers.User?> GetUser(string user)
        {
            if (UserCache.TryGetValue(user, out var twitchUser))
            {
                if (twitchUser != null)
                {
                    return twitchUser;
                }
            }

            var users = await _twitchApi.Helix.Users.GetUsersAsync(null, new List<string> { user }, _configuration["twitchAccessToken"]);
            var userObj = users.Users.FirstOrDefault();
            UserCache[user] = userObj;
            return userObj;
        }

        public async Task<Follower?> GetUserFollow(string user)
        {
            var broadcasterId = await GetBroadcasterUserId();
            var userId = await GetUserId(user);
            if (userId == null) return null;
            try
            {
                var response = await _twitchApi.Helix.Users.GetUsersFollowsAsync(null, null, 1, userId, broadcasterId, _configuration["twitchAccessToken"]);
                if (!response.Follows.Any())
                {
                    return null;
                }
                var firstFollower = response.Follows.First();
                return new Follower()
                {
                    Username = firstFollower.FromLogin,
                    DisplayName = firstFollower.FromLogin,
                    FollowDate = firstFollower.FollowedAt
                };
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing IsStreamOnline(): {0}", error);
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
                return response.Data.Any();
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing IsStreamOnline(): {0}", error);
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
                var response = await _twitchApi.Helix.Moderation.GetModeratorsAsync(broadcasterId, new List<string> { userId });
                return response.Data.Any();
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing IsStreamOnline(): {0}", error);
            }
            return false;
        }

        public async Task<bool> IsStreamOnline()
        {
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting stream status.");
            try
            {
                var streams = await _twitchApi.Helix.Streams.GetStreamsAsync(userIds: new List<string>() { userId }, accessToken: _configuration["twitchAccessToken"]);
                if (streams.Streams == null)
                {
                    return false;
                }

                return streams.Streams.Count() > 0;
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing IsStreamOnline(): {0}", error);
            }
            return false;
        }

        public async Task<bool> IsStreamOnline(string userId)
        {
            try
            {
                var streams = await _twitchApi.Helix.Streams.GetStreamsAsync(userIds: new List<string>() { userId }, accessToken: _configuration["twitchAccessToken"]);
                if (streams.Streams == null)
                {
                    return false;
                }

                return streams.Streams.Count() > 0;
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing IsStreamOnline(userId): {0}", error);
            }
            return false;
        }

        public async Task<List<string>> AreStreamsOnline(List<string> userIds)
        {
            try
            {
                var streams = await _twitchApi.Helix.Streams.GetStreamsAsync(userIds: userIds, first: 100, accessToken: _configuration["twitchAccessToken"]);
                if (streams.Streams == null)
                {
                    return new List<string>();
                }
                return streams.Streams.Select(x => x.UserId).ToList();
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing AreStreamsOnline: {0}", error);
            }
            return new List<string>();
        }

        public async Task<DateTime> StreamStartedAt()
        {
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting stream status.");
            try
            {
                var streams = await _twitchApi.Helix.Streams.GetStreamsAsync(userIds: new List<string>() { userId }, accessToken: _configuration["twitchAccessToken"]);
                if (streams.Streams.Count() == 0)
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
                _logger.LogError("Error doing getting stream started at: {0}", error);
            }
            return DateTime.MinValue;
        }

        public async Task<int> GetViewerCount()
        {
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting stream status.");
            try
            {
                var streams = await _twitchApi.Helix.Streams.GetStreamsAsync(userIds: new List<string>() { userId }, accessToken: _configuration["twitchAccessToken"]);
                if (streams.Streams.Count() == 0)
                {
                    return 0;
                }
                return streams.Streams.First().ViewerCount;
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing getting viewer count: {0}", error);
            }
            return 0;
        }

        public async Task<string> GetCurrentGame()
        {
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting stream status.");
            return await GetCurrentGame(userId);
        }

        public async Task<string> GetCurrentGame(string userId)
        {
            try
            {
                var channelInfo = await _twitchApi.Helix.Channels.GetChannelInformationAsync(userId, _configuration["twitchAccessToken"]);
                if (channelInfo.Data.Length > 0)
                {
                    return channelInfo.Data[0].GameName;
                }
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing Getting current game: {0}", error);
            }
            return "";
        }

        public async Task<string> GetStreamTitle()
        {
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting stream status.");
            try
            {
                var channelInfo = await _twitchApi.Helix.Channels.GetChannelInformationAsync(userId, _configuration["twitchAccessToken"]);
                if (channelInfo.Data.Length > 0)
                {
                    return channelInfo.Data[0].Title;
                }
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing Getting stream title: {0}", error);
            }
            return "";
        }

        public async Task<string> GetStreamThumbnail()
        {
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting stream status.");
            try
            {
                var streamInfo = await _twitchApi.Helix.Streams.GetStreamsAsync(userIds: new List<string> { userId }, accessToken: _configuration["twitchAccessToken"]);
                if (streamInfo.Streams.Count() > 0)
                {
                    var stream = streamInfo.Streams.First();
                    return stream.ThumbnailUrl;
                }
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing Getting Thumbnail: {0}", error);
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
                _logger.LogError("Error doing Raid: {0}", error);
            }
        }

        public async Task ShoutoutStreamer(string userId)
        {
            var broadcasterId = await GetBroadcasterUserId() ?? throw new Exception("Error getting broadcaster id.");
            try
            {
                await _twitchApi.Helix.Chat.SendShoutoutAsync(broadcasterId, userId, broadcasterId, _configuration["twitchAccessToken"]);
            }
            catch (HttpResponseException ex)
            {
                var error = await ex.HttpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Error doing shoutout: {0}", error);
            }
        }

        public async Task TimeoutUser(string name, int length, string reason)
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

        public async Task SubscribeToAllTheStuffs(string sessionId)
        {
            await ValidateAndRefreshToken();
            var userId = await GetBroadcasterUserId();
            if (userId == null) return;

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.follow", "2",
                new Dictionary<string, string>{
                    {"broadcaster_user_id", userId},
                    {"moderator_user_id", userId}
                },
                TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                sessionId, accessToken: _configuration["twitchAccessToken"]
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.subscribe", "1",
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                sessionId, accessToken: _configuration["twitchAccessToken"]
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.subscription.end", "1",
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                sessionId, accessToken: _configuration["twitchAccessToken"]
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.subscription.gift", "1",
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                sessionId, accessToken: _configuration["twitchAccessToken"]
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.subscription.message", "1",
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                sessionId, accessToken: _configuration["twitchAccessToken"]
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.cheer", "1",
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                sessionId, accessToken: _configuration["twitchAccessToken"]
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.channel_points_custom_reward_redemption.add",
                "1",
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                sessionId, accessToken: _configuration["twitchAccessToken"]
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "stream.offline",
                "1",
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                sessionId, accessToken: _configuration["twitchAccessToken"]
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "stream.online",
                "1",
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                sessionId, accessToken: _configuration["twitchAccessToken"]
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.raid",
                "1",
                new Dictionary<string, string>{{"to_broadcaster_user_id", userId},
                },
                TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                sessionId, accessToken: _configuration["twitchAccessToken"]
            );

            // Maybe do a from also and switch this handling raid events


        }

        public async Task ValidateAndRefreshToken()
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                var validToken = await _twitchApi.Auth.ValidateAccessTokenAsync(_configuration["twitchAccessToken"]);
                if (validToken != null && validToken.ExpiresIn > 1200)
                {
                    TimeSpan.FromSeconds(validToken.ExpiresIn);
                    _settingsFileManager.AddOrUpdateAppSetting("expiresIn", validToken.ExpiresIn);
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
                        _settingsFileManager.AddOrUpdateAppSetting("twitchAccessToken", refreshToken.AccessToken);
                        _settingsFileManager.AddOrUpdateAppSetting("twitchRefreshToken", refreshToken.RefreshToken);
                        _settingsFileManager.AddOrUpdateAppSetting("expiresIn", refreshToken.ExpiresIn.ToString());
                    }
                    catch (Exception e)
                    {
                        _logger.LogError("Error refreshing token: {0}", e.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when validing/refreshing token");
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }


    }
}