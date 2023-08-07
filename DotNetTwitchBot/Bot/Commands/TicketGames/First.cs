using System.Data.SqlTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Commands.Features;

namespace DotNetTwitchBot.Bot.Commands.TicketGames
{
    public class First : BaseCommandService
    {
        private readonly TicketsFeature _ticketsFeature;
        private readonly ILogger<First> _logger;
        private readonly List<string> GotTickets = new();

        public First(
            TicketsFeature ticketsFeature,
            ServiceBackbone serviceBackbone,
            CommandHandler commandHandler,
            ILogger<First> logger
            ) : base(serviceBackbone, commandHandler)
        {
            _ticketsFeature = ticketsFeature;
            _logger = logger;
        }

        public override async Task Register()
        {
            var moduleName = "First";
            await RegisterDefaultCommand("first", this, moduleName, Rank.Viewer);
            _logger.LogInformation($"Registered commands for {moduleName}");
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "first":
                    if (!GotTickets.Contains(e.Name)) throw new SkipCooldownException();
                    _ = await _ticketsFeature.GiveTicketsToViewer(e.Name, Tools.RandomRange(1, 4));

                    GotTickets.Add(e.Name);
                    break;
            }
        }
    }
}