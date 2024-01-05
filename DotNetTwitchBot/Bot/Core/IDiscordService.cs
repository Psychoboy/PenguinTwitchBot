using Discord;

namespace DotNetTwitchBot.Bot.Core
{
    public interface IDiscordService
    {
        Task LogAsync(LogMessage message);
        ConnectionState ServiceStatus();
    }
}