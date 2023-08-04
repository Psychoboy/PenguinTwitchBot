using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using System.Timers;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class AddActive : BaseCommandService
    {
        private TicketsFeature _ticketsFeature;
        Timer _ticketsToActiveCommandTimer;
        private long _ticketsToGiveOut = 0;
        private DateTime _lastTicketsAdded = DateTime.Now;
        private readonly ILogger<AddActive> _logger;

        public AddActive(
            ILogger<AddActive> logger,
            ServiceBackbone eventService,
            TicketsFeature ticketsFeature,
            IServiceScopeFactory scopeFactory,
            CommandHandler commandHandler
        ) : base(eventService, scopeFactory, commandHandler)
        {
            _ticketsFeature = ticketsFeature;
            _logger = logger;
            _ticketsToActiveCommandTimer = new Timer(1000);
            _ticketsToActiveCommandTimer.Elapsed += OnActiveCommandTimerElapsed;
            _ticketsToActiveCommandTimer.Start();
        }

        public override async Task Register()
        {
            var moduleName = "AddActive";
            await RegisterDefaultCommand("addactive", this, moduleName, Rank.Streamer);
            _logger.LogInformation($"Registered commands for {moduleName}");
        }

        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = _commandHandler.GetCommand(e.Command);
            if (command == null) return Task.CompletedTask;
            switch (command.CommandProperties.CommandName)
            {
                case "addactive":
                    {
                        if (Int64.TryParse(e.Args[0], out long amount))
                        {
                            AddActiveTickets(amount);
                        }
                        break;
                    }
            }
            return Task.CompletedTask;
        }

        public void AddActiveTickets(long amount)
        {
            if (amount > 100) amount = 100;
            _lastTicketsAdded = DateTime.Now;
            _ticketsToGiveOut += amount;
        }

        private async void OnActiveCommandTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (_ticketsToGiveOut > 0 && _lastTicketsAdded.AddSeconds(5) < DateTime.Now)
            {
                await _ticketsFeature.GiveTicketsToActiveUsers(_ticketsToGiveOut);
                await _serviceBackbone.SendChatMessage(string.Format("Sending {0:n0} tickets to all active users.", _ticketsToGiveOut));
                _ticketsToGiveOut = 0;
            }
        }
    }
}