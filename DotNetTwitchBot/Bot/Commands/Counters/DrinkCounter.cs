using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands.Counters
{
    public class DrinkCounter : BaseCounter
    {
        public DrinkCounter(ServiceBackbone eventService, SendAlerts sendAlerts) : base(eventService, sendAlerts)
        {
        }

        public override string Message()
        {
            throw new NotImplementedException();
        }

        protected override Task OnCommand(object? sender, CommandEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}