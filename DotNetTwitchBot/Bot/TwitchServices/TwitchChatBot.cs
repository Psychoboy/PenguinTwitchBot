using DotNetTwitchBot.Bot.TwitchServices.TwitchModels;
using DotNetTwitchBot.Extensions;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.Moderation.CheckAutoModStatus;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.TwitchServices
{
    /// <summary>
    /// Everything here is executed as the Twitch Bot configured.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="configuration"></param>
    /// <param name="twitchService"></param>
    /// <param name="messageIdTracker"></param>
    /// <param name="settingsFileManager"></param>
    public class TwitchChatBot(
        ILogger<TwitchChatBot> logger,
         IConfiguration configuration,
         ITwitchService twitchService,
         ChatMessageIdTracker messageIdTracker,
         SettingsFileManager settingsFileManager) : ITwitchChatBot
    {
        private readonly TwitchAPI _twitchApi = new();

        private readonly Timer HealthStatusTimer = new();
        static readonly SemaphoreSlim semaphoreSlim = new(1, 1);

        public void SetAccessToken(string accessToken)
        {
            _twitchApi.Settings.AccessToken = accessToken;
        }

        public Task<bool> IsConnected()
        {
            return ValidateAndRefreshToken();
        }

        private async void HealthStatusTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            await ValidateAndRefreshToken();
        }

        public async Task SendMessage(string message, bool sourceOnly = true)
        {
            if (message.Length == 0)
            {
                logger.LogWarning("Message is empty, not sending");
                return;
            }
            
            try
            {
                var chunks = message.SplitInParts(450);
                foreach (var chunk in chunks)
                {
                    //var result = await _twitchApi.Helix.Chat.SendChatMessage(await twitchService.GetBroadcasterUserId(), await twitchService.GetBotUserId(), chunk);
                    var msg = new TwitchLib.Api.Helix.Models.Channels.SendChatMessage.SendChatMessageRequest
                    {
                        BroadcasterId = await twitchService.GetBroadcasterUserId(),
                        SenderId = await twitchService.GetBotUserId(),
                        Message = chunk,
                        ForSourceOnly = sourceOnly
                    };
                    var result = await _twitchApi.Helix.Chat.SendChatMessage(msg);
                    messageIdTracker.AddMessageId(result.Data.First().MessageId);
                    if (result.Data.First().IsSent == false)
                    {
                        logger.LogWarning("Message failed to send: {reason}", result.Data.First().DropReason.Message);
                        return;
                    }
                    else
                    {
                        logger.LogInformation("BOTCHATMSG: {message}", chunk.Replace(Environment.NewLine, ""));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send message. {message}", message);
            }
        }

        public async Task ReplyToMessage(string name, string messageId, string message, bool sourceOnly = true)
        {
            if (message.Length == 0)
            {
                logger.LogWarning("Message is empty, not sending");
                return;
            }
            
            try
            {
                var chunks = message.SplitInParts(450);
                foreach (var chunk in chunks)
                {
                    var msg = new TwitchLib.Api.Helix.Models.Channels.SendChatMessage.SendChatMessageRequest
                    {
                        BroadcasterId = await twitchService.GetBroadcasterUserId(),
                        SenderId = await twitchService.GetBotUserId(),
                        Message = chunk,
                        ReplyParentMessageId = messageId,
                        ForSourceOnly = sourceOnly
                    };
                    var result = await _twitchApi.Helix.Chat.SendChatMessage(msg);
                    messageIdTracker.AddMessageId(result.Data.First().MessageId);
                    if (result.Data.First().IsSent == false)
                    {
                        logger.LogWarning("Message failed to send: {reason}", result.Data.First().DropReason.Message);
                        return;
                    }
                    else
                    {
                        logger.LogInformation("BOTREPLYCHATMSG: {name} - {message}", name, chunk.Replace(Environment.NewLine, ""));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send message. {message}", message);
            }
        }

        private async Task<bool> ValidateAndRefreshToken()
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                var validToken = await _twitchApi.Auth.ValidateAccessTokenAsync(configuration["twitchBotAccessToken"]);
                if (validToken != null && validToken.ExpiresIn > 1200)
                {
                    await settingsFileManager.AddOrUpdateAppSetting("botExpiresIn", validToken.ExpiresIn);
                    return true;
                }
                else
                {
                    try
                    {
                        logger.LogInformation("Refreshing Bot Token");

                        //var refreshToken = await _twitchApi.Auth.RefreshAuthTokenAsync(configuration["twitchBotRefreshToken"], configuration["twitchBotClientSecret"], configuration["twitchBotClientId"]);
                        //configuration["twitchBotAccessToken"] = refreshToken.AccessToken;
                        //configuration["botExpiresIn"] = refreshToken.ExpiresIn.ToString();
                        //configuration["twitchBotRefreshToken"] = refreshToken.RefreshToken;
                        //_twitchApi.Settings.AccessToken = refreshToken.AccessToken;
                        //await settingsFileManager.AddOrUpdateAppSetting("twitchBotAccessToken", refreshToken.AccessToken);
                        //await settingsFileManager.AddOrUpdateAppSetting("twitchBotRefreshToken", refreshToken.RefreshToken);
                        //await settingsFileManager.AddOrUpdateAppSetting("botExpiresIn", refreshToken.ExpiresIn.ToString());
                        //return true;
                    }
                    catch (Exception e)
                    {
                        logger.LogError("Error refreshing bot token: {error}", e.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error when validing/refreshing token");
            }
            finally
            {
                semaphoreSlim.Release();
            }
            return false;
        }

        public async Task<bool> RefreshAccessToken()
        {
            var url = "https://id.twitch.tv/oauth2/token";
            var formData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("client_id", configuration["twitchBotClientId"]),
                new KeyValuePair<string, string>("client_secret", configuration["twitchBotClientSecret"]),
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
            };
            var encodedContent = new FormUrlEncodedContent(formData);
            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.PostAsync(url, encodedContent);
                    if (response.IsSuccessStatusCode)
                    {
                        logger.LogInformation("Successfully requested bot access token");
                        var content = await response.Content.ReadAsStringAsync();
                        var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<TwitchTokenResponse>(content);
                        if(tokenResponse == null)
                        {
                            logger.LogError("Failed to deserialize token response");
                            return false;
                        }
                        configuration["twitchBotAccessToken"] = tokenResponse.AccessToken;
                        configuration["botExpiresIn"] = tokenResponse.ExpiresIn.ToString();
                        _twitchApi.Settings.AccessToken = tokenResponse.AccessToken;
                        await settingsFileManager.AddOrUpdateAppSetting("twitchBotAccessToken", tokenResponse.AccessToken);
                        await settingsFileManager.AddOrUpdateAppSetting("botExpiresIn", tokenResponse.ExpiresIn.ToString());
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error requesting bot access token");
                }
            }
            return false;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting Twitch Chat Bot");
            HealthStatusTimer.Interval = 30000;
            HealthStatusTimer.Elapsed += HealthStatusTimer_Elapsed;

            _twitchApi.Settings.ClientId = configuration["twitchBotClientId"];
            _twitchApi.Settings.AccessToken = configuration["twitchBotAccessToken"];
            _twitchApi.Settings.Secret = configuration["twitchBotClientSecret"];
            _twitchApi.Settings.Scopes = [];
            foreach (var authScope in Enum.GetValues(typeof(AuthScopes)))
            {
                if ((AuthScopes)authScope == AuthScopes.Any) continue;
                _twitchApi.Settings.Scopes.Add((AuthScopes)authScope);
            }

            await ValidateAndRefreshToken();
            HealthStatusTimer.Start();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopping Twitch Chat Bot");
            HealthStatusTimer.Stop();
            HealthStatusTimer.Elapsed -= HealthStatusTimer_Elapsed;
            return Task.CompletedTask;
        }
    }
}
