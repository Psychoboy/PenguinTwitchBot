using PenguinTwitchBot.Bot.TwitchServices;

namespace PenguinTwitchBot.Bot.Commands.Moderation
{
    public interface IModeratorFilterService
    {
        Task<bool> IsPermittedAsync(string message);
    }

    public class ModeratorFilterService(
        Blacklist blacklist,
        ITwitchService twitchService,
        ILogger<ModeratorFilterService> logger) : IModeratorFilterService
    {
        public async Task<bool> IsPermittedAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return true;

            if (blacklist.IsBlacklisted(message))
            {
                logger.LogWarning("Message rejected by local Blacklist filter: {Message}", message);
                return false;
            }

            try
            {
                if (!await twitchService.WillBePermittedByAutomod(message))
                {
                    logger.LogWarning("Message rejected by Twitch AutoMod filter: {Message}", message);
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking Twitch AutoMod filter; rejecting message as unsafe.");
                return false;
            }

            return true;
        }
    }
}

