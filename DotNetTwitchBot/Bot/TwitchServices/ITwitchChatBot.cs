namespace DotNetTwitchBot.Bot.TwitchServices
{
    public interface ITwitchChatBot : IHostedService
    {
        Task<bool> IsConnected();
        void SetAccessToken(string accessToken);
    }
}