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

namespace DotNetTwitchBot.Bot.TwitchServices
{
    public class TwitchService
    {
        private readonly TwitchAPI _twitchApi = new TwitchAPI();
        private ILogger<TwitchService> _logger;
        private IConfiguration _configuration;
        private HttpClient _httpClient = new HttpClient();
        Timer _timer;
        public TwitchService(ILogger<TwitchService> logger, IConfiguration configuration)
        {

            _logger = logger;
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
            // await ValidateAndRefreshBotToken();
        }

        public async Task<List<Subscription>> GetAllSubscriptions()
        {
            await ValidateAndRefreshToken();
            var userId = await GetBroadcasterUserId();
            if (userId == null)
            {
                throw new Exception("Error getting user id.");
            }

            List<Subscription> subs = new List<Subscription>();
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
            var users = await _twitchApi.Helix.Users.GetUsersAsync(null, new List<string> { broadcaster }, _configuration["twitchAccessToken"]);

            return users.Users.FirstOrDefault()?.Id;
        }

        public async Task<string?> GetBotUserId()
        {
            var broadcaster = _configuration["botName"];
            if (broadcaster == null) return null;
            var users = await _twitchApi.Helix.Users.GetUsersAsync(null, new List<string> { broadcaster }, _configuration["twitchBotAccessToken"]);

            return users.Users.FirstOrDefault()?.Id;
        }

        public async Task<string?> GetUserId(string user)
        {
            var users = await _twitchApi.Helix.Users.GetUsersAsync(null, new List<string> { user }, _configuration["twitchAccessToken"]);
            return users.Users.FirstOrDefault()?.Id;
        }

        public async Task<Follower?> GetUserFollow(string user)
        {
            var broadcasterId = await GetBroadcasterUserId();
            var userId = await GetUserId(user);
            if (userId == null) return null;
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

        public async Task<List<Follower>> GetAllFollows()
        {
            await ValidateAndRefreshToken();
            var broadcasterId = await GetBroadcasterUserId();
            var after = "";
            List<Follower> followers = new List<Follower>();
            while (true)
            {
                var response = await _twitchApi.Helix.Users.GetUsersFollowsAsync(after, null, 100, null, broadcasterId, _configuration["twitchAccessToken"]);
                followers.AddRange(response.Follows.Select(x => new Follower()
                {
                    Username = x.FromLogin,
                    DisplayName = x.FromUserName,
                    FollowDate = x.FollowedAt
                }));
                after = response.Pagination.Cursor;
                if (string.IsNullOrEmpty(response.Pagination.Cursor))
                {
                    break;
                }
            }
            return followers;
        }

        public async Task<bool> IsStreamOnline()
        {
            var userId = await GetBroadcasterUserId();
            if (userId == null)
            {
                throw new Exception("Error getting stream status.");
            }
            var streams = await _twitchApi.Helix.Streams.GetStreamsAsync(userIds: new List<string>() { userId }, accessToken: _configuration["twitchAccessToken"]);
            if (streams.Streams == null)
            {
                return false;
            }

            return streams.Streams.Count() > 0;
        }

        public async Task<DateTime> StreamStartedAt()
        {
            var userId = await GetBroadcasterUserId();
            if (userId == null)
            {
                throw new Exception("Error getting stream status.");
            }
            var streams = await _twitchApi.Helix.Streams.GetStreamsAsync(userIds: new List<string>() { userId }, accessToken: _configuration["twitchAccessToken"]);
            if (streams.Streams.Count() == 0)
            {
                return DateTime.MinValue;
            }
            var stream = streams.Streams.First();
            var startTime = stream.StartedAt;
            return startTime;
        }

        // public async Task TestWhisper(string target, string message)
        // {
        //     await ValidateAndRefreshBotToken();
        //     var botId = await GetBotUserId();
        //     if (botId == null) return;
        //     var userId = await GetUserId(target);
        //     if (userId == null) return;
        //     var accessToken = _configuration["twitchBotAccessToken"];
        //     await _twitchApi.Helix.Whispers.SendWhisperAsync(botId, userId, message, true, accessToken);
        // }

        public async Task<string> GetCurrentGame()
        {
            var userId = await GetBroadcasterUserId();
            if (userId == null)
            {
                throw new Exception("Error getting stream status.");
            }
            var channelInfo = await _twitchApi.Helix.Channels.GetChannelInformationAsync(userId, _configuration["twitchAccessToken"]);
            if (channelInfo.Data.Length > 0)
            {
                return channelInfo.Data[0].GameName;
            }

            return "";
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
        }

        public async Task ValidateAndRefreshToken()
        {
            try
            {
                var validToken = await _twitchApi.Auth.ValidateAccessTokenAsync(_configuration["twitchAccessToken"]);
                if (validToken != null && validToken.ExpiresIn > 1200)
                {
                    var expiresIn = TimeSpan.FromSeconds(validToken.ExpiresIn);
                    AddOrUpdateAppSetting("expiresIn", validToken.ExpiresIn);
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
                        AddOrUpdateAppSetting("twitchAccessToken", refreshToken.AccessToken);
                        AddOrUpdateAppSetting("twitchRefreshToken", refreshToken.RefreshToken);
                        AddOrUpdateAppSetting("expiresIn", refreshToken.ExpiresIn.ToString());
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
        }

        public async Task ValidateAndRefreshBotToken()
        {
            try
            {
                var validToken = await _twitchApi.Auth.ValidateAccessTokenAsync(_configuration["twitchBotAccessToken"]);
                if (validToken != null && validToken.ExpiresIn > 1200)
                {
                    var expiresIn = TimeSpan.FromSeconds(validToken.ExpiresIn);
                    AddOrUpdateAppSetting("botExpiresIn", validToken.ExpiresIn);
                }
                else
                {
                    try
                    {
                        _logger.LogInformation("Refreshing Bot Token");
                        var refreshToken = await _twitchApi.Auth.RefreshAuthTokenAsync(_configuration["twitchBotRefreshToken"], _configuration["twitchBotClientSecret"], _configuration["twitchBotClientId"]);
                        _configuration["twitchBotAccessToken"] = refreshToken.AccessToken;
                        _configuration["botExpiresIn"] = refreshToken.ExpiresIn.ToString();
                        _configuration["twitchBotRefreshToken"] = refreshToken.RefreshToken;
                        //_twitchApi.Settings.AccessToken = refreshToken.AccessToken;
                        AddOrUpdateAppSetting("twitchBotAccessToken", refreshToken.AccessToken);
                        AddOrUpdateAppSetting("twitchBotRefreshToken", refreshToken.RefreshToken);
                        AddOrUpdateAppSetting("botExpiresIn", refreshToken.ExpiresIn.ToString());
                    }
                    catch (Exception e)
                    {
                        _logger.LogError("Error refreshing bot token: {0}", e.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when validing/refreshing bot token");
            }
        }


        public void AddOrUpdateAppSetting<T>(string sectionPathKey, T value)
        {
            try
            {
                var filePath = _configuration["Secrets:SecretsConf"]; //Path.Combine(AppContext.BaseDirectory, "appsettings.secrets.json");
                if (filePath == null) throw new Exception("Invalid file configuration");
                string json = File.ReadAllText(filePath);
                dynamic jsonObj = JsonConvert.DeserializeObject(json) ?? throw new InvalidOperationException();

                SetValueRecursively(sectionPathKey, jsonObj, value);

                string output = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(filePath, output);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating settings");
            }
        }

        private void SetValueRecursively<T>(string sectionPathKey, dynamic jsonObj, T value)
        {
            // split the string at the first ':' character
            var remainingSections = sectionPathKey.Split(":", 2);

            var currentSection = remainingSections[0];
            if (remainingSections.Length > 1)
            {
                // continue with the process, moving down the tree
                var nextSection = remainingSections[1];
                SetValueRecursively(nextSection, jsonObj[currentSection], value);
            }
            else
            {
                // we've got to the end of the tree, set the value
                jsonObj[currentSection] = value;
            }
        }
    }
}