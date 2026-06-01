using PenguinTwitchBot.Bot.TwitchServices.TwitchModels;
using PenguinTwitchBot.TwitchApi.Auth;
using PenguinTwitchBot.TwitchApi.Helix;
using PenguinTwitchBot.TwitchApi.Models.Chat;
using PenguinTwitchBot.Extensions;
using Timer = System.Timers.Timer;

namespace PenguinTwitchBot.Bot.TwitchServices
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
         IAuthClient authClient,
         IChatClient chatClient,
         ChatMessageIdTracker messageIdTracker,
         SettingsFileManager settingsFileManager) : ITwitchChatBot
    {
        private readonly Timer HealthStatusTimer = new();
        static readonly SemaphoreSlim semaphoreSlim = new(1, 1);
        private volatile string _accessToken = configuration["twitchBotAccessToken"] ?? string.Empty;

        public void SetAccessToken(string accessToken)
        {
            _accessToken = accessToken;
            configuration["twitchBotAccessToken"] = accessToken;
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
                    var broadcasterId = await twitchService.GetBroadcasterUserId() ?? throw new InvalidOperationException("Broadcaster ID is unavailable.");
                    var senderId = await twitchService.GetBotUserId() ?? throw new InvalidOperationException("Bot user ID is unavailable.");
                    var request = new SendChatMessageRequest
                    {
                        BroadcasterId = broadcasterId,
                        SenderId = senderId,
                        Message = chunk,
                        ForSourceOnly = sourceOnly,
                    };

                    var result = await chatClient.SendChatMessageAsync(
                        configuration["twitchBotClientId"]!,
                        _accessToken,
                        request);

                    var first = result.Data.FirstOrDefault();
                    if (first == null)
                    {
                        logger.LogWarning("Message failed to send: no response payload");
                        return;
                    }

                    messageIdTracker.AddMessageId(first.MessageId);
                    if (first.IsSent == false)
                    {
                        logger.LogWarning("Message failed to send: {reason}", first.DropReason?.Message);
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
                    var broadcasterId = await twitchService.GetBroadcasterUserId() ?? throw new InvalidOperationException("Broadcaster ID is unavailable.");
                    var senderId = await twitchService.GetBotUserId() ?? throw new InvalidOperationException("Bot user ID is unavailable.");
                    var request = new SendChatMessageRequest
                    {
                        BroadcasterId = broadcasterId,
                        SenderId = senderId,
                        Message = chunk,
                        ReplyParentMessageId = messageId,
                        ForSourceOnly = sourceOnly,
                    };

                    var result = await chatClient.SendChatMessageAsync(
                        configuration["twitchBotClientId"]!,
                        _accessToken,
                        request);

                    var first = result.Data.FirstOrDefault();
                    if (first == null)
                    {
                        logger.LogWarning("Reply failed to send: no response payload");
                        return;
                    }

                    messageIdTracker.AddMessageId(first.MessageId);
                    if (first.IsSent == false)
                    {
                        logger.LogWarning("Message failed to send: {reason}", first.DropReason?.Message);
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
                var validToken = await authClient.ValidateAccessTokenAsync(_accessToken);
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
                        return await RefreshAccessToken();
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
                new("client_id", configuration["twitchBotClientId"]),
                new("client_secret", configuration["twitchBotClientSecret"]),
                new("grant_type", "client_credentials"),
            };
            var encodedContent = new FormUrlEncodedContent(formData);
            using var client = new HttpClient();
            try
            {
                var response = await client.PostAsync(url, encodedContent);
                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("Successfully requested bot access token");
                    var content = await response.Content.ReadAsStringAsync();
                    var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<TwitchTokenResponse>(content);
                    if (tokenResponse == null)
                    {
                        logger.LogError("Failed to deserialize token response");
                        return false;
                    }
                    configuration["twitchBotAccessToken"] = tokenResponse.AccessToken;
                    configuration["botExpiresIn"] = tokenResponse.ExpiresIn.ToString();
                    _accessToken = tokenResponse.AccessToken;
                    await settingsFileManager.AddOrUpdateAppSetting("twitchBotAccessToken", tokenResponse.AccessToken);
                    await settingsFileManager.AddOrUpdateAppSetting("botExpiresIn", tokenResponse.ExpiresIn.ToString());
                }
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error requesting bot access token");
            }
            return false;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting Twitch Chat Bot");
            HealthStatusTimer.Interval = 30000;
            HealthStatusTimer.Elapsed += HealthStatusTimer_Elapsed;

            _accessToken = configuration["twitchBotAccessToken"] ?? string.Empty;

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
