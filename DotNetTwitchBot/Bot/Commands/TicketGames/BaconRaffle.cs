using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.TicketGames
{
    public class BaconRaffle : BaseRaffle
    {
        private ILogger<BaconRaffle> _logger;

        public BaconRaffle(
            ServiceBackbone eventService,
            TicketsFeature ticketsFeature,
            IServiceScopeFactory scopeFactory,
            CommandHandler commandHandler,
            ILogger<BaconRaffle> logger
            ) : base(eventService, ticketsFeature, scopeFactory, commandHandler, "sptvBacon", "!bacon", "bacon")
        {
            _logger = logger;
        }

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
            NumberOfWinners = Tools.Next(min, max + 1);
        }

        public override async Task Register()
        {
            var moduleName = "BaconRaffle";
            await RegisterDefaultCommand("raffle", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("bacon", this, moduleName, Rank.Viewer);
            _logger.LogInformation($"Registered commands for {moduleName}");
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
                        await EnterRaffle(e);
                    }
                    break;
            }
        }
    }
}