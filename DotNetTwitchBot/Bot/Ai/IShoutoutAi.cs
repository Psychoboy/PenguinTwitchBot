
namespace DotNetTwitchBot.Bot.Ai
{
    public interface IShoutoutAi
    {
        Task<string> GetShoutoutForStreamer(string streamerName, string gameName, string streamTitle, string bio, string additionalInfo = "");
    }
}