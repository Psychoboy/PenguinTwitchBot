using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events.Chat;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.TicketGames
{
    public class BaconRaffle(
        IServiceBackbone eventService,
        IPointsSystem pointsSystem,
        ICommandHandler commandHandler,
        IMediator mediator,
        ILogger<BaconRaffle> logger
            ) : BaseRaffle(eventService, pointsSystem, commandHandler, "sptvBacon", "!bacon", "bacon", mediator, logger)
    {
        protected override void UpdateNumberOfWinners()
        {
            var entries = NumberEntered;
            var max = 3;
            var min = 1;
            switch (entries)
            {
                case < 10:
                    break;
                case >= 10 and < 15:
                    max = 4;
                    break;
                case >= 15 and < 20:
                    min = 2;
                    break;
                case >= 20:
                    max = 5;
                    break;
            }
            NumberOfWinners = StaticTools.Next(min, max + 1);
        }

        public override async Task Register()
        {
            var moduleName = "BaconRaffle";
            await RegisterDefaultCommand("raffle", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("bacon", this, moduleName, Rank.Viewer);
            await _pointSystem.RegisterDefaultPointForGame("raffle");
            logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "raffle":
                    {
                        if (e.Args.Count == 0) return;
                        if (Int32.TryParse(e.Args[0], out int amount))
                        {
                            await StartRaffle(e.Name, amount);
                        }
                    }
                    break;

                case "bacon":
                    {
                        EnterRaffle(e);
                    }
                    break;
            }
        }
    }
}