namespace DotNetTwitchBot.Bot.TwitchServices
{
    public interface ITwitchChatBot : IHostedService
    {
        Task<bool> IsConnected();
        Task ReplyToMessage(string messageId, string message);
        Task SendMessage(string message);
        void SetAccessToken(string accessToken);
    }
}