
using PenguinTwitchBot.Bot.Events;

namespace PenguinTwitchBot.Bot.TwitchServices
{
    public interface ITwitchWebsocketHostedService : IHostedService
    {
        Task AdBreak(AdBreakStartEventArgs e);
        Task Reconnect();
        Task StreamOffline();
        Task StreamOnline();
    }
}