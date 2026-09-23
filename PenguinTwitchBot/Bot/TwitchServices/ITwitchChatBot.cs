using PenguinTwitchBot.TwitchApi.Models.Chat;

namespace PenguinTwitchBot.Bot.TwitchServices
{
    public interface ITwitchChatBot : IHostedService
    {
        Task<bool> IsConnected();
        Task<bool> RefreshAccessToken();
        Task ReplyToMessage(string name, string messageId, string message, bool sourceOnly = true);
        Task<SendChatMessageResult?> SendMessage(string message, bool sourceOnly = true);
        void SetAccessToken(string accessToken);
    }
}