using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands.TicketGames
{
    public class WaffleRaffle : BaseRaffle
    {
        public WaffleRaffle(
            ServiceBackbone eventService,
            TicketsFeature ticketsFeature
        ) : base(eventService, ticketsFeature, "sptvWaffle", "!waffle", "waffle")
        {
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch (e.Command)
            {
                case "testwafflestart":
                    {
                        if (e.Args.Count == 0) return;
                        if (!_serviceBackbone.IsBroadcasterOrBot(e.Name)) return;
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