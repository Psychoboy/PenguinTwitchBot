using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public class GiveawayFeature : BaseFeature
    {
        private IGiveawayEntries _giveawayEntries;

        public GiveawayFeature(
            EventService eventService,
            IGiveawayEntries giveawayEntries
            ) : base(eventService)
        {
            _giveawayEntries = giveawayEntries;
            eventService.CommandEvent += OnCommandEvent;
        }

        private Task OnCommandEvent(object? sender, CommandEventArgs e)
        {
            switch(e.Command) {
                case "testenter":{
                    break;
                }
                case "testdraw": {
                    break;
                }
                case "testresetdraw": {
                    break;
                }
                case "testprize": {
                    break;
                }
            }

            return Task.CompletedTask;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }
    }
}