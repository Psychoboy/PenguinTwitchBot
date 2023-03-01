namespace DotNetTwitchBot.Bot.Commands.Features
{
    public abstract class BaseFeature : BackgroundService
    {
        public BaseFeature(CommandService commandService) {
            CommandService = commandService;
        }

        public CommandService CommandService { get; }

        public async Task SendChatMessage(string message) 
        {
            await CommandService.SendChatMessage(message);
        }
    }
}
