using Discord.WebSocket;

namespace DotNetTwitchBot.Application.Discord
{
    public class DiscordReadyNotification(DiscordSocketClient client) : Notifications.INotification
    {
        public DiscordSocketClient Client { get; private set; } = client;
    }
}
