using DotNetTwitchBot.Bot.TwitchServices;
using KickLib;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DotNetTwitchBot.Bot.KickServices
{
    public class KickService : IKickService
    {
        private readonly IKickApi kickApi;
        private readonly ILogger<KickService> logger;
        private readonly ChatMessageIdTracker chatMessageIdTracker;
        private int broadcasterUserId = -1;
        private static readonly HttpClient httpClient = new();

        public KickService(
            ILogger<KickService> logger,
            ILoggerFactory loggerFactory,
            ChatMessageIdTracker chatMessageIdTracker,
            IConfiguration configuration)
        {
            this.logger = logger;
            var streamerClientId = configuration["Kick:Streamer:ClientId"];
            var streamerClientSecret = configuration["Kick:Streamer:ClientSecret"];
            var streamerAccessToken = configuration["Kick:Streamer:AccessToken"];
            var streamerRefreshToken = configuration["Kick:Streamer:RefreshToken"];
            var streamerSettings = new KickLib.Core.ApiSettings
            {
                ClientId = streamerClientId,
                ClientSecret = streamerClientSecret,
                RefreshToken = streamerRefreshToken,
                AccessToken = streamerAccessToken
            };

            kickApi = KickApi.Create(streamerSettings, loggerFactory);
            this.chatMessageIdTracker = chatMessageIdTracker;
            logger.LogInformation("KickService initialized.");
        }

        public void SetTokens(string accessToken, string refreshToken)
        {
            kickApi.ApiSettings.AccessToken = accessToken;
            kickApi.ApiSettings.RefreshToken = refreshToken;
        }

        public async Task<int> GetBroadcasterUserId()
        {
            if(broadcasterUserId != -1)
            {
                return broadcasterUserId;
            }

            var userResult = await kickApi.Users.GetMeAsync();
            if(userResult != null && userResult.IsSuccess)
            {
                broadcasterUserId = userResult.Value.UserId;
                return broadcasterUserId;
            }
            return broadcasterUserId;
        }

        public async Task SendMessage(string message)
        {
            try
            {
                var result = await kickApi.Chat.SendMessageAsBotAsync(message);
                if (result != null)
                {
                    if (result.IsSuccess)
                    {
                        if (string.IsNullOrEmpty(result.Value.MessageId) == false)
                        {
                            chatMessageIdTracker.AddMessageId(result.Value.MessageId);
                        }
                        logger.LogInformation("BOTMSG[K]: {message}", message.Replace(Environment.NewLine, ""));
                    }
                    else
                    {
                        logger.LogWarning("Failed to send message as streamer to Kick chat. Error: {Error}", result.Errors.ToString());
                    }
                    return;
                }
                else
                {
                    logger.LogWarning("Failed to send message as streamer to Kick chat. Result was null.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending message as streamer to Kick chat.");
            }
        }

        public async Task SendMessageAsStreamer(string message)
        {
            try
            {
                var result = await kickApi.Chat.SendMessageAsUserAsync(await GetBroadcasterUserId(), message);
                if (result != null)
                {
                    if (result.IsSuccess)
                    {
                        if (string.IsNullOrEmpty(result.Value.MessageId) == false)
                        {
                            chatMessageIdTracker.AddMessageId(result.Value.MessageId);
                        }
                        logger.LogInformation("STREAMERMSG[K]: {message}", message.Replace(Environment.NewLine, ""));
                    }
                    else
                    {
                        logger.LogWarning("Failed to send message as streamer to Kick chat. Error: {Error}", result.Errors.ToString());
                    }
                    return;
                }
                else
                {
                    logger.LogWarning("Failed to send message as streamer to Kick chat. Result was null.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending message as streamer to Kick chat.");
            }
        }

        public async Task ReplyToMessage(string name, string messageId, string message)
        {
            try
            {
                var result = await kickApi.Chat.SendMessageAsBotAsync(message, messageId);
                if (result != null)
                {
                    if (result.IsSuccess)
                    {
                        if (string.IsNullOrEmpty(result.Value.MessageId) == false)
                        {
                            chatMessageIdTracker.AddMessageId(result.Value.MessageId);
                        }
                        logger.LogInformation("BOTREPLYMSG[K]: {name} - {message}", name, message.Replace(Environment.NewLine, ""));
                    }
                    else
                    {
                        logger.LogWarning("Failed to send reply message as streamer to Kick chat. Error: {Error}", result.Errors.ToString());
                    }
                    return;
                }
                else
                {
                    logger.LogWarning("Failed to send reply message as streamer to Kick chat. Result was null.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending reply message as streamer to Kick chat.");
            }
        }

        public async Task<Models.KickUser?> GetViewerInfoByUserId(string userId)
        {
            try
            {
                var viewer = await kickApi.Users.GetUserAsync(int.Parse(userId));
                if(viewer == null || !viewer.IsSuccess)
                {
                    logger.LogWarning("Failed to get viewer info by user ID from Kick. Error: {Error}", viewer?.Errors.ToString());
                    return null;
                }

                return await GetViewerInfoByUsername(viewer.Value.Name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting viewer info by user ID from Kick.");
                return null;
            }
        }

        public async Task<Models.KickUser?> GetViewerInfoByUsername(string username)
        {
            try
            {
                var url = $"https://kick.com/api/v2/channels/superpenguintv/users/{username}";
                using Stream stream = await httpClient.GetStreamAsync(url);
                var kickUser = await JsonSerializer.DeserializeAsync<Models.KickUser>(stream);
                return kickUser;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting viewer info by username from Kick.");
                return null;
            }
        }

        public async Task<Follower?> GetFollower(string username)
        {
            var kickUser = await GetViewerInfoByUsername(username);
            if (kickUser != null && kickUser.FollowingSince != null)
            {
                return new Follower
                {
                    UserId = kickUser.Id.ToString(),
                    Username = kickUser.Username,
                    DisplayName = kickUser.Username,
                    FollowDate = (DateTime)kickUser.FollowingSince,
                    Platform = PlatformType.Kick
                };
            }
            return null;
        }
    }
}
