using System.Security.Claims;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Commands.Features;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class First : BaseCommand
    {
        private List<string> ClaimedFirst {get;} = new List<string>();
        private int MaxClaims = 60;
        private TicketsFeature _ticketsFeature;

        public First(
            ServiceBackbone eventService,
            TicketsFeature ticketsFeature
        ) : base(eventService)
        {
            _ticketsFeature = ticketsFeature;
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch(e.Command) {
                case "testfirst": {
                    await giveFirst(e.Sender);

                }
                break;
                case "testresetfirst": {
                    if(!_eventService.IsBroadcasterOrBot(e.Sender)) return;
                    ResetFirst();
                }
                break;
            }
        }

        private void ResetFirst()
        {
            ClaimedFirst.Clear();
        }

        private async Task giveFirst(string sender)
        {
            if(!_eventService.IsOnline) {
                await SendChatMessage(sender, "Nice try, the stream is currently offline.");
                return;
            }

            if(ClaimedFirst.Count >= MaxClaims) {
                await SendChatMessage(sender, "Sorry, You were to slow today. FeelsBadMan");
                return;
            }

            if(ClaimedFirst.Contains(sender.ToLower())) return;

            ClaimedFirst.Add(sender.ToLower());
            var awardPoints = Tools.CurrentThreadRandom.Next(1,3);
            await _ticketsFeature.GiveTicketsToViewer(sender, awardPoints);
            await SendChatMessage(sender, string.Format("Whooohooo! You came in position {0} and get {1} tickets!! PogChamp", ClaimedFirst.Count, awardPoints));
        }
    }
}