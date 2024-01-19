using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.TicketGames
{
    public class PancakeRaffle(
        IServiceBackbone eventService,
        ITicketsFeature ticketsFeature,
        ICommandHandler commandHandler,
        ILogger<PancakeRaffle> logger
        ) : BaseRaffle(eventService, ticketsFeature, commandHandler, "sptvPancake", "!pancake", "pancake", logger)
    {
        public override async Task Register()
        {
            var moduleName = "PancakeRaffle";
            await RegisterDefaultCommand("pancakeraffle", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("pancake", this, moduleName, Rank.Viewer);
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "pancakeraffle":
                    {
                        if (e.Args.Count == 0) return;
                        if (Int32.TryParse(e.Args[0], out int amount))
                        {
                            await StartRaffle(e.Name, amount);
                        }
                    }
                    break;

                case "pancake":
                    {
                        EnterRaffle(e);
                    }
                    break;
            }
        }
    }
}