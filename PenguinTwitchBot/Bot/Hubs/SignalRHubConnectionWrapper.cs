using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace PenguinTwitchBot.Bot.Hubs
{
    public class SignalRHubConnectionWrapper : ISignalRHubConnection
    {
        private readonly HubConnection _connection;

        public SignalRHubConnectionWrapper(HubConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public void On<T>(string eventName, Func<T, Task> handler)
        {
            _connection.On<T>(eventName, handler);
        }

        public Task StartAsync()
        {
            return _connection.StartAsync();
        }

        public ValueTask DisposeAsync()
        {
            return _connection.DisposeAsync();
        }
    }
}
