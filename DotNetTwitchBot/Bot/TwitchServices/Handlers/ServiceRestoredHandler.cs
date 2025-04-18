using DotNetTwitchBot.Bot.TwitchServices.TwitchModels;
using MediatR;

namespace DotNetTwitchBot.Bot.TwitchServices.Handlers
{
    public class ServiceRestoredHandler(ITwitchWebsocketHostedService twitchWebsocket) : INotificationHandler<ServiceRestored>
    {
        public async Task Handle(ServiceRestored request, CancellationToken cancellationToken)
        {
            await twitchWebsocket.Reconnect();
        }
    }
}
