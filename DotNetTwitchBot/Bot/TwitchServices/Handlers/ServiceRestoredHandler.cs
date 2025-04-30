using DotNetTwitchBot.Bot.TwitchServices.TwitchModels;
using MediatR;

namespace DotNetTwitchBot.Bot.TwitchServices.Handlers
{
    public class ServiceRestoredHandler(ITwitchWebsocketHostedService twitchWebsocket) : INotificationHandler<ServiceRestored>
    {
        public async Task Handle(ServiceRestored request, CancellationToken cancellationToken)
        {
            Thread.Sleep(3000); // Wait for 3 seconds before reconnecting to Twitch WebSocket to ensure the service is fully restored
            await twitchWebsocket.Reconnect();
        }
    }
}
