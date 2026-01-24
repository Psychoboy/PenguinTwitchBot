using KickLib;
using Microsoft.Extensions.Logging;

namespace DotNetTwitchBot.Bot.KickServices
{
    public class KickService : IKickService
    {
        private readonly IKickApi kickApi;
        private readonly ILogger<KickService> logger;
        private int broadcasterUserId = -1;

        public KickService(
            ILogger<KickService> logger,
            ILoggerFactory loggerFactory,
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
                        logger.LogInformation("STREAMERCHATMSG[K]: {message}", message.Replace(Environment.NewLine, ""));
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
    }
}
