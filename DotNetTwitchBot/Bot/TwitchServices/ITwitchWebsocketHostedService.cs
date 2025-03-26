
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.TwitchServices
{
    public interface ITwitchWebsocketHostedService : IHostedService
    {
        Task AdBreak(AdBreakStartEventArgs e);
        Task Reconnect();
        Task StreamOffline();
        Task StreamOnline();
    }
}