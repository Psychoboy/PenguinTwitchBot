using Microsoft.AspNetCore.SignalR.Client;

namespace PenguinTwitchBot.Bot.Hubs
{
    public interface ISignalRHubConnectionFactory
    {
        ISignalRHubConnection CreateMainHubConnection(Uri absoluteUri);
    }
}
