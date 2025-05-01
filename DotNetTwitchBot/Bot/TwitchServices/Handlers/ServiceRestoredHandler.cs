using DotNetTwitchBot.Bot.TwitchServices.TwitchModels;
using MediatR;

namespace DotNetTwitchBot.Bot.TwitchServices.Handlers
{
    public class ServiceRestoredHandler(ITwitchWebsocketHostedService twitchWebsocket) : INotificationHandler<ServiceRestored>
    {
        private const int ReconnectDelayMilliseconds = 3000; // Delay duration in milliseconds

        public async Task Handle(ServiceRestored request, CancellationToken cancellationToken)
        {
            await Task.Delay(ReconnectDelayMilliseconds, cancellationToken); // Wait for 3 seconds before reconnecting to Twitch WebSocket to ensure the service is fully restored
            await twitchWebsocket.Reconnect();
        }
    }
}
