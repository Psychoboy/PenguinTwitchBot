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
    public class TwitchBotService
    {
        private readonly TwitchAPI _twitchApi = new TwitchAPI();
        private ILogger<TwitchBotService> _logger;
        private IConfiguration _configuration;
        private HttpClient _httpClient = new HttpClient();
        Timer _timer;
        private SettingsFileManager _settingsFileManager;

        public TwitchBotService(ILogger<TwitchBotService> logger, IConfiguration configuration, SettingsFileManager settingsFileManager)
        {

            _logger = logger;
            _settingsFileManager = settingsFileManager;
            _configuration = configuration;
            _twitchApi.Settings.ClientId = _configuration["twitchBotClientId"];
            _twitchApi.Settings.AccessToken = _configuration["twitchBotAccessToken"];
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

        public async Task SendWhisper(string target, string message)
        {
            try
            {
                await ValidateAndRefreshBotToken();
                var botId = await GetBotUserId();
                if (botId == null) return;
                var userId = await GetUserId(target);
                if (userId == null) return;
                var accessToken = _configuration["twitchBotAccessToken"];
                await _twitchApi.Helix.Whispers.SendWhisperAsync(botId, userId, message, true, accessToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending whisper");
            }
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
            var users = await _twitchApi.Helix.Users.GetUsersAsync(null, new List<string> { user }, _configuration["twitchBotAccessToken"]);
            return users.Users.FirstOrDefault()?.Id;
        }

        private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            await ValidateAndRefreshBotToken();
        }

        public async Task ValidateAndRefreshBotToken()
        {
            try
            {
                var validToken = await _twitchApi.Auth.ValidateAccessTokenAsync(_configuration["twitchBotAccessToken"]);
                if (validToken != null && validToken.ExpiresIn > 1200)
                {
                    var expiresIn = TimeSpan.FromSeconds(validToken.ExpiresIn);
                    _settingsFileManager.AddOrUpdateAppSetting("botExpiresIn", validToken.ExpiresIn);
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
                        _settingsFileManager.AddOrUpdateAppSetting("twitchBotAccessToken", refreshToken.AccessToken);
                        _settingsFileManager.AddOrUpdateAppSetting("twitchBotRefreshToken", refreshToken.RefreshToken);
                        _settingsFileManager.AddOrUpdateAppSetting("botExpiresIn", refreshToken.ExpiresIn.ToString());
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
    }
}