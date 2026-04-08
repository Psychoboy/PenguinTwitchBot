using Discord.WebSocket;

namespace DotNetTwitchBot.Application.Discord
{
    public class DiscordConnectedNotification(DiscordSocketClient client) : Notifications.INotification
    {
        public DiscordSocketClient Client { get; private set; } = client;
    }
}
