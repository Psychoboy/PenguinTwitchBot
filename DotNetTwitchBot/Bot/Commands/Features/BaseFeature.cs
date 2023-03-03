using DotNetTwitchBot.Bot.Core;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public abstract class BaseFeature : IHostedService
    {
        public BaseFeature(EventService eventService) {
            _eventService = eventService;
        }

        protected EventService _eventService { get; }

        public async Task SendChatMessage(string message) 
        {
            await _eventService.SendChatMessage(message);
        }

        public virtual Task StartAsync(CancellationToken cancellationToken){return Task.CompletedTask;}
        public virtual Task StopAsync(CancellationToken cancellationToken){return Task.CompletedTask;}
    }
}
