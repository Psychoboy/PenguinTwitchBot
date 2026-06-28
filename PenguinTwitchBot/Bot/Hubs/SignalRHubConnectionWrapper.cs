using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace PenguinTwitchBot.Bot.Hubs
{
    public class SignalRHubConnectionWrapper : ISignalRHubConnection
    {
        private readonly HubConnection _connection;
        private bool _started;

        public SignalRHubConnectionWrapper(HubConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public IDisposable On<T>(string eventName, Func<T, Task> handler)
        {
            return _connection.On<T>(eventName, handler);
        }

        public async Task StartAsync()
        {
            if (_started) return;

            const int maxRetries = 5;
            var delay = TimeSpan.FromMilliseconds(500);

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    await _connection.StartAsync();
                    _started = true;
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    // Fix: Check if this was the last allowed attempt
                    if (attempt >= maxRetries - 1)
                    {
                        throw; // Rethrow the final exception to notify the caller immediately
                    }

                    await Task.Delay(delay);
                    delay = TimeSpan.FromTicks(delay.Ticks * 2);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            // Note: Components using '.On()' should dispose of their returned IDisposable 
            // tokens individually in their own Dispose/DisposeAsync lifecycles.
            await _connection.DisposeAsync();
        }
    }
}
