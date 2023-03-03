using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.Subscriptions;

namespace DotNetTwitchBot.Bot
{
    public class TwitchService
    {
        private readonly TwitchAPI _twitchApi = new TwitchAPI();
        private ILogger<TwitchService> _logger;
        private IConfiguration _configuration;
        private HttpClient _httpClient = new HttpClient();
        public TwitchService(ILogger<TwitchService> logger, IConfiguration configuration) {

            _logger = logger;
            _configuration = configuration;
            _twitchApi.Settings.ClientId = _configuration["twitchClientId"];
            _twitchApi.Settings.AccessToken = _configuration["twitchAccessToken"];
            _twitchApi.Settings.Scopes = new List<AuthScopes>();
            foreach(var authScope in Enum.GetValues(typeof(AuthScopes))) {
                if((AuthScopes)authScope == AuthScopes.Any) continue;
                _twitchApi.Settings.Scopes.Add((AuthScopes)authScope);
            }

        }

        public async Task<List<Subscription>> GetAllSubscriptions() {
            await ValidateAndRefreshToken();
            var userId = await GetBroadcasterUserId();
            if(userId == null) {
                throw new Exception("Error getting user id.");
            }

            List<Subscription> subs = new List<Subscription>();
            var after = "";
            while(true){
                var curSubs = await _twitchApi.Helix.Subscriptions.GetBroadcasterSubscriptionsAsync(userId, 100, after);
                if(curSubs != null) {
                    subs.AddRange(curSubs.Data);
                    if(string.IsNullOrEmpty(curSubs.Pagination.Cursor)) {
                        break;
                    }
                    after = curSubs.Pagination.Cursor;
                }    
            }
            return subs;
        }

        public async Task<string?> GetBroadcasterUserId() {
            await ValidateAndRefreshToken();
            var users = await _twitchApi.Helix.Users.GetUsersAsync(null, new List<string>{_configuration["broadcaster"]},_configuration["twitchAccessToken"]);
            
            return users.Users.FirstOrDefault()?.Id;
        }

        public async Task<string?> GetUserId(string user) {
            await ValidateAndRefreshToken();
            var users = await _twitchApi.Helix.Users.GetUsersAsync(null, new List<string>{user},_configuration["twitchAccessToken"]);
            return users.Users.FirstOrDefault()?.Id;
        }

        public async Task<bool> IsStreamOnline() {
            await ValidateAndRefreshToken();
            var userId = await GetBroadcasterUserId();
            if(userId == null) {
                throw new Exception("Error getting stream status.");
            }
            var streams = await _twitchApi.Helix.Streams.GetStreamsAsync(userIds: new List<string>(){userId});
            if(streams.Streams == null) {
                return false;
            }

            return streams.Streams.Count() > 0;
        }

        public async Task SubscribeToAllTheStuffs(string sessionId) {
            await ValidateAndRefreshToken();
            var userId = await GetBroadcasterUserId();
            if(userId == null) return;

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.follow", "2", 
                new Dictionary<string, string>{
                    {"broadcaster_user_id", userId},
                    {"moderator_user_id", userId}
                },
                TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                sessionId
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.subscribe", "1", 
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                sessionId
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.subscription.gift", "1", 
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                sessionId
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.subscription.message", "1", 
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                sessionId
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.cheer", "1", 
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                sessionId
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.channel_points_custom_reward_redemption.add",
                "1",
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                sessionId
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "stream.offline",
                "1",
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                sessionId
            );

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "stream.online",
                "1",
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                sessionId
            );
        }

        public async Task ValidateAndRefreshToken() {
            var validToken = await _twitchApi.Auth.ValidateAccessTokenAsync();
            if(validToken != null && validToken.ExpiresIn > 120) {
                var expiresIn = TimeSpan.FromSeconds(validToken.ExpiresIn);
                SettingsHelpers.AddOrUpdateAppSetting("expiresIn",validToken.ExpiresIn);
                _logger.LogInformation("Token expires in {0}", expiresIn.ToString("hh\\:mm\\:ss"));
            } else {
            try{
                var refreshToken = await _twitchApi.Auth.RefreshAuthTokenAsync(_configuration["twitchRefreshToken"], _configuration["twitchClientSecret"], _configuration["twitchClientId"]);
                _configuration["twitchAccessToken"] = refreshToken.AccessToken;
                _configuration["expiresIn"] = refreshToken.ExpiresIn.ToString();
                _configuration["twitchRefreshToken"] = refreshToken.RefreshToken;
                _twitchApi.Settings.AccessToken = refreshToken.AccessToken;
                SettingsHelpers.AddOrUpdateAppSetting("twitchAccessToken", refreshToken.AccessToken);
                SettingsHelpers.AddOrUpdateAppSetting("twitchRefreshToken", refreshToken.RefreshToken);
                SettingsHelpers.AddOrUpdateAppSetting("expiresIn",refreshToken.ExpiresIn.ToString());
                } catch(Exception e){
                    _logger.LogError("Error refreshing token: {0}", e.Message);
                }
            }
        }
    }
}