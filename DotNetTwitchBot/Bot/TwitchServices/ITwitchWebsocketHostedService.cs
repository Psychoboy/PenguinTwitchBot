
namespace DotNetTwitchBot.Bot.TwitchServices
{
    public interface ITwitchWebsocketHostedService : IHostedService
    {
        Task ForceReconnect();
    }
}