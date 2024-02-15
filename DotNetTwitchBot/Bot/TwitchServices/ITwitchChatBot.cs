namespace DotNetTwitchBot.Bot.TwitchServices
{
    public interface ITwitchChatBot
    {
        Task<bool> IsConnected();
    }
}