using System.Data.SqlTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Commands.Features;

namespace DotNetTwitchBot.Bot.Commands.TicketGames
{
    public class First : BaseCommand
    {
        private List<string> Firsts = new List<string>();
        private TicketsFeature _ticketsFeature;
        private List<string> GotTickets = new List<string>();

        public First(
            TicketsFeature ticketsFeature,
            ServiceBackbone serviceBackbone) : base(serviceBackbone)
        {
            _ticketsFeature = ticketsFeature;
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch (e.Command)
            {
                case "first":
                    if (!GotTickets.Contains(e.Name)) return;
                    var tickets = await _ticketsFeature.GiveTicketsToViewer(e.Name, Tools.RandomRange(1, 4));

                    GotTickets.Add(e.Name);
                    break;
            }
        }
    }
}