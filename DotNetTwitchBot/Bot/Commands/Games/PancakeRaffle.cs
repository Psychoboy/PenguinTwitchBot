using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands.Games
{
    public class PancakeRaffle : BaseRaffle
    {
        public PancakeRaffle(
            ServiceBackbone eventService,
            TicketsFeature ticketsFeature
        ) : base(eventService, ticketsFeature, "sptvPancake", "!pancake", "pancake")
        {
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch (e.Command) {
                case "testpancakestart": {
                    if(e.Args.Count == 0) return;
                    if(!_eventService.IsBroadcasterOrBot(e.Sender)) return;
                    if(Int32.TryParse(e.Args[0], out int amount)) {
                        await StartRaffle(e.Sender, amount);
                    }
                }
                break;

                case "pancake": {
                    await EnterRaffle(e.Sender);
                }
                break;
            }
        }
    }
}