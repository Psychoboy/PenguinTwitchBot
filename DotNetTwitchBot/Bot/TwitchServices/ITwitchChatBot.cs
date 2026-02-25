namespace DotNetTwitchBot.Bot.TwitchServices
{
    public interface ITwitchChatBot : IHostedService
    {
        Task<bool> IsConnected();
        Task<bool> RefreshAccessToken();
        Task ReplyToMessage(string name, string messageId, string message, bool sourceOnly = true);
        Task SendMessage(string message, bool sourceOnly = true);
        void SetAccessToken(string accessToken);
    }
}