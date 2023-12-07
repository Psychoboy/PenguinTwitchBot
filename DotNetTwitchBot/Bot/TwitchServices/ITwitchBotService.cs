namespace DotNetTwitchBot.Bot.TwitchServices
{
    public interface ITwitchBotService
    {
        Task<string?> GetBotUserId();
        Task<string?> GetUserId(string user);
        Task SendWhisper(string target, string message);
        Task ValidateAndRefreshBotToken();
        bool IsServiceUp();
    }
}