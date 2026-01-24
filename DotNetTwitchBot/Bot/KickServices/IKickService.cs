
namespace DotNetTwitchBot.Bot.KickServices
{
    public interface IKickService
    {
        Task ReplyToMessage(string name, string messageId, string message);
        Task SendMessage(string message);
        Task SendMessageAsStreamer(string message);
        void SetTokens(string accessToken, string refreshToken);
    }
}