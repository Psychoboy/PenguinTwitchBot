using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TwitchLib.Api;

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
        }

        public async Task<string?> GetBroadcasterUserId() {
            var users = await _twitchApi.Helix.Users.GetUsersAsync(null, new List<string>{_configuration["broadcaster"]});
            return users.Users.FirstOrDefault()?.Id;
        }

        public async Task SubscribeToChannelRedemptionAddEvents(string sessionId) {
            await ValidateAndRefreshToken();
            var userId = await GetBroadcasterUserId();
            if(userId == null) return;

            await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.channel_points_custom_reward_redemption.add",
                "1",
                new Dictionary<string, string>{{"broadcaster_user_id", userId},
                },
                TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                sessionId
            );
        }

        public async Task ValidateAndRefreshToken() {
            var validToken = await _twitchApi.Auth.ValidateAccessTokenAsync();
            var expiresIn = TimeSpan.FromSeconds(validToken.ExpiresIn);
            SettingsHelpers.AddOrUpdateAppSetting("expiresIn",validToken.ExpiresIn);
            _logger.LogInformation("Token expires in {0}", expiresIn.ToString("hh\\:mm\\:ss"));
            var refreshToken = await _twitchApi.Auth.RefreshAuthTokenAsync(_configuration["twitchRefreshToken"], _configuration["twitchClientSecret"]);
            SettingsHelpers.AddOrUpdateAppSetting("twitchAccessToken", refreshToken.AccessToken);
            SettingsHelpers.AddOrUpdateAppSetting("expiresIn",refreshToken.ExpiresIn);
        }
    }
}