using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class DuelGame : BaseCommand
    {
        public DuelGame(ServiceBackbone serviceBackbone) : base(serviceBackbone)
        {
        }

        protected override Task OnCommand(object? sender, CommandEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}