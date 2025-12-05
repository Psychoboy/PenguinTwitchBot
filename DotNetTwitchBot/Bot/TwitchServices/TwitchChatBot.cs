using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
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

        public async Task SendMessage(string message)
        {
            if (message.Length == 0)
            {
                logger.LogWarning("Message is empty, not sending");
                return;
            }
            try
            {
                var result = await _twitchApi.Helix.Chat.SendChatMessage(await twitchService.GetBroadcasterUserId(), await twitchService.GetBotUserId(), message);
                messageIdTracker.AddMessageId(result.Data.First().MessageId);
                if (result.Data.First().IsSent == false)
                {
                    logger.LogWarning("Message failed to send: {reason}", result.Data.First().DropReason.Message);
                }
                else
                {
                    logger.LogInformation("BOTCHATMSG: {message}", message.Replace(Environment.NewLine, ""));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send message. {message}", message);
            }
        }

        public async Task ReplyToMessage(string messageId, string message)
        {
            if (message.Length == 0)
            {
                logger.LogWarning("Message is empty, not sending");
                return;
            }
            try
            {
                var result = await _twitchApi.Helix.Chat.SendChatMessage(await twitchService.GetBroadcasterUserId(), await twitchService.GetBotUserId(), message, messageId);
                messageIdTracker.AddMessageId(result.Data.First().MessageId);
                if (result.Data.First().IsSent == false)
                {
                    logger.LogWarning("Message failed to send: {reason}", result.Data.First().DropReason.Message);
                }
                else
                {
                    logger.LogInformation("BOTCHATMSG: {message}", message.Replace(Environment.NewLine, ""));
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
                    //await settingsFileManager.AddOrUpdateAppSetting("botExpiresIn", validToken.ExpiresIn);
                    return true;
                }
                else
                {
                    try
                    {
                        logger.LogInformation("Refreshing Bot Token");

                        var refreshToken = await _twitchApi.Auth.RefreshAuthTokenAsync(configuration["twitchBotRefreshToken"], configuration["twitchBotClientSecret"], configuration["twitchBotClientId"]);
                        configuration["twitchBotAccessToken"] = refreshToken.AccessToken;
                        configuration["botExpiresIn"] = refreshToken.ExpiresIn.ToString();
                        configuration["twitchBotRefreshToken"] = refreshToken.RefreshToken;
                        _twitchApi.Settings.AccessToken = refreshToken.AccessToken;
                        await settingsFileManager.AddOrUpdateAppSetting("twitchBotAccessToken", refreshToken.AccessToken);
                        await settingsFileManager.AddOrUpdateAppSetting("twitchBotRefreshToken", refreshToken.RefreshToken);
                        await settingsFileManager.AddOrUpdateAppSetting("botExpiresIn", refreshToken.ExpiresIn.ToString());
                        return true;
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

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting Twitch Chat Bot");
            HealthStatusTimer.Interval = 30000;
            HealthStatusTimer.Elapsed += HealthStatusTimer_Elapsed;

            _twitchApi.Settings.ClientId = configuration["twitchBotClientId"];
            _twitchApi.Settings.AccessToken = configuration["twitchBotAccessToken"];
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
