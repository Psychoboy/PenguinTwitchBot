using Discord.WebSocket;

namespace PenguinTwitchBot.Application.Discord
{
    public class DiscordConnectedNotification(DiscordSocketClient client) : Notifications.INotification
    {
        public DiscordSocketClient Client { get; private set; } = client;
    }
}
