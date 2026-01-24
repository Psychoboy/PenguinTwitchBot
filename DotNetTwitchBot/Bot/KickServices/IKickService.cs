
namespace DotNetTwitchBot.Bot.KickServices
{
    public interface IKickService
    {
        Task SendMessage(string message);
        void SetTokens(string accessToken, string refreshToken);
    }
}