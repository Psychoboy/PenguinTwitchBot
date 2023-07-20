using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.TicketGames
{
    public class PancakeRaffle : BaseRaffle
    {
        public PancakeRaffle(
            ServiceBackbone eventService,
            TicketsFeature ticketsFeature,
            IServiceScopeFactory scopeFactory,
            CommandHandler commandHandler
        ) : base(eventService, ticketsFeature, scopeFactory, commandHandler, "sptvPancake", "!pancake", "pancake")
        {
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch (e.Command)
            {
                case "pancakeraffle":
                    {
                        if (e.Args.Count == 0) return;
                        if (!_serviceBackbone.IsBroadcasterOrBot(e.Name)) return;
                        if (Int32.TryParse(e.Args[0], out int amount))
                        {
                            await StartRaffle(e.Name, amount);
                        }
                    }
                    break;

                case "pancake":
                    {
                        await EnterRaffle(e);
                    }
                    break;
            }
        }

        public override void RegisterDefaultCommands()
        {
            throw new NotImplementedException();
        }
    }
}