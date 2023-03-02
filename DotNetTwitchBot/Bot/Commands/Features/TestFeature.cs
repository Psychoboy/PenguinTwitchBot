using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public class TestFeature : BaseFeature
    {
        public TestFeature(EventService commandService) : base(commandService)
        {
            commandService.CommandEvent += OnCommand;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }

        private async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = e.Command;
            switch(command) {
                case "test":
                    await SendChatMessage("Test message received");
                    break;
            }
        }
    }
}