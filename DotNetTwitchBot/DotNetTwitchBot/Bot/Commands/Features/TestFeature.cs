namespace DotNetTwitchBot.Bot.Commands.Features
{
    public class TestFeature : BaseFeature
    {
        public TestFeature(CommandService commandService) : base(commandService)
        {
            commandService.CommandEvent += OnCommand;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }

        private void OnCommand(object? sender, CommandEventArgs e)
        {
            var command = e.Command;
            switch(command) {
                case "test":
                    SendChatMessage("Test message received");
                    break;
            }
        }
    }
}