namespace DotNetTwitchBot.Bot.TwitchServices
{
    public interface ITwitchChatBot : IHostedService
    {
        Task<bool> IsConnected();
        Task SendMessage(string message);
        void SetAccessToken(string accessToken);
    }
}