using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands
{
    public abstract class BaseCommand : IHostedService
    {
        public BaseCommand(ServiceBackbone eventService)
        {
            _eventService = eventService;
            eventService.CommandEvent += OnCommand;
        }

        protected ServiceBackbone _eventService { get; }

        public async Task SendChatMessage(string message)
        {
            await _eventService.SendChatMessage(message);
        }

        public async Task SendChatMessage(string name, string message) {
                await _eventService.SendChatMessage(name, message);
        }

        public virtual Task StartAsync(CancellationToken cancellationToken) { return Task.CompletedTask; }
        public virtual Task StopAsync(CancellationToken cancellationToken) { return Task.CompletedTask; }

        protected abstract Task OnCommand(object? sender, CommandEventArgs e);
    }
}
