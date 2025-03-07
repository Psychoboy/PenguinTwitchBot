
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.TwitchServices
{
    public interface ITwitchWebsocketHostedService : IHostedService
    {
        Task AdBreak(AdBreakStartEventArgs e);
        Task ForceReconnect();
        Task StreamOffline();
        Task StreamOnline();
    }
}