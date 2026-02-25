
using DotNetTwitchBot.Bot.KickServices.Models;

namespace DotNetTwitchBot.Bot.KickServices
{
    public interface IKickService
    {
        Task<Follower?> GetFollower(string username);
        Task<KickUser?> GetViewerInfoByUserId(string userId);
        Task<KickUser?> GetViewerInfoByUsername(string username);
        Task ReplyToMessage(string name, string messageId, string message);
        Task SendMessage(string message);
        Task SendMessageAsStreamer(string message);
        void SetTokens(string accessToken, string refreshToken);
    }
}