using DotNetTwitchBot.Bot.Core;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public abstract class BaseFeature : BackgroundService
    {
        public BaseFeature(EventService eventService) {
            _eventService = eventService;
        }

        protected EventService _eventService { get; }

        public async Task SendChatMessage(string message) 
        {
            await _eventService.SendChatMessage(message);
        }
    }
}
