using Discord.WebSocket;
using MediatR;

namespace DotNetTwitchBot.Application.Discord
{
    public class DiscordConnectedNotification(DiscordSocketClient client) : INotification
    {
        public DiscordSocketClient Client { get; private set; } = client;
    }
}
