using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands.Games
{
    public class BaconRaffle : BaseRaffle
    {
        public BaconRaffle(ServiceBackbone eventService, TicketsFeature ticketsFeature) : base(eventService, ticketsFeature, "sptvBacon", "!bacon", "bacon")
        {
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
            NumberOfWinners = Tools.CurrentThreadRandom.Next(min, max);
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch (e.Command)
            {
                case "testbaconstart":
                    {
                        if (e.Args.Count == 0) return;
                        if (!_eventService.IsBroadcasterOrBot(e.Name)) return;
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