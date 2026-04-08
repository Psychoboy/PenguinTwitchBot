using Discord.WebSocket;
using DotNetTwitchBot.Application.Notifications;

namespace DotNetTwitchBot.Application.Discord
{
    public class DiscordReadyNotification(DiscordSocketClient client) : INotification
    {
        public DiscordSocketClient Client { get; private set; } = client;
    }
}
