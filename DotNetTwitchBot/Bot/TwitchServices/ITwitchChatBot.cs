namespace DotNetTwitchBot.Bot.TwitchServices
{
    public interface ITwitchChatBot
    {
        bool IsConnected();
        bool IsInChannel();
    }
}