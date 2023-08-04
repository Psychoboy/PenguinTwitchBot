using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.TicketGames
{
    public class WaffleRaffle : BaseRaffle
    {
        private readonly ILogger<WaffleRaffle> _logger;

        public WaffleRaffle(
            ServiceBackbone eventService,
            TicketsFeature ticketsFeature,
            IServiceScopeFactory scopeFactory,
            CommandHandler commandHandler,
            ILogger<WaffleRaffle> logger
        ) : base(eventService, ticketsFeature, scopeFactory, commandHandler, "sptvWaffle", "!waffle", "waffle")
        {
            _logger = logger;
        }

        public override async Task Register()
        {
            var moduleName = "WaffleRaffle";
            await RegisterDefaultCommand("waffleraffle", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("waffle", this, moduleName, Rank.Viewer);
            _logger.LogInformation($"Registered commands for {moduleName}");
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = _commandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "waffleraffle":
                    {
                        if (e.Args.Count == 0) return;
                        if (Int32.TryParse(e.Args[0], out int amount))
                        {
                            await StartRaffle(e.Name, amount);
                        }
                    }
                    break;

                case "waffle":
                    {
                        await EnterRaffle(e);
                    }
                    break;
            }
        }
    }
}