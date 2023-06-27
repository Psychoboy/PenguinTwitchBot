using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using System.Timers;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class AddActive : BaseCommand
    {
        private TicketsFeature _ticketsFeature;
        Timer _ticketsToActiveCommandTimer;
        private long _ticketsToGiveOut = 0;
        private DateTime _lastTicketsAdded = DateTime.Now;

        public AddActive(
            ServiceBackbone eventService,
            TicketsFeature ticketsFeature
        ) : base(eventService)
        {
            _ticketsFeature = ticketsFeature;
            _ticketsToActiveCommandTimer = new Timer(1000);
            _ticketsToActiveCommandTimer.Elapsed += OnActiveCommandTimerElapsed;
            _ticketsToActiveCommandTimer.Start();
        }

        protected override Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch (e.Command)
            {
                case "addactive":
                    {
                        if (!_serviceBackbone.IsBroadcasterOrBot(e.Name)) return Task.CompletedTask;
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