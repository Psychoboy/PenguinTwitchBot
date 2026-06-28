using Microsoft.AspNetCore.SignalR.Client;

namespace PenguinTwitchBot.Bot.Hubs
{
    public class SignalRHubConnectionFactory : ISignalRHubConnectionFactory
    {
        public ISignalRHubConnection CreateMainHubConnection(Uri absoluteUri)
        {
            var concreteConnection = new HubConnectionBuilder()
                .WithUrl(absoluteUri)
                .WithAutomaticReconnect()
                .Build();

            // Fix: Return the wrapper which implements ISignalRHubConnection
            return new SignalRHubConnectionWrapper(concreteConnection);
        }
    }
}
