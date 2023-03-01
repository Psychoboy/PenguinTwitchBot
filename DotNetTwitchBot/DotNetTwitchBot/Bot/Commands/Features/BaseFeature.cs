namespace DotNetTwitchBot.Bot.Commands.Features
{
    public abstract class BaseFeature : BackgroundService
    {
        public BaseFeature(CommandService commandService) {
            CommandService = commandService;
        }

        public CommandService CommandService { get; }

        public void SendChatMessage(string message) 
        {
            CommandService.SendChatMessage(message);
        }
    }
}
