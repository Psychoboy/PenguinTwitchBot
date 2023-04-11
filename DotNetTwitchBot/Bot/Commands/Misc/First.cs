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
        private List<string> ClaimedFirst { get; } = new List<string>();
        private int MaxClaims = 60;
        private static int CurrentClaims = 0;
        private TicketsFeature _ticketsFeature;
        private ILogger<First> _logger;

        public First(
            ServiceBackbone eventService,
            ILogger<First> logger,
            TicketsFeature ticketsFeature
        ) : base(eventService)
        {
            _ticketsFeature = ticketsFeature;
            _logger = logger;
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch (e.Command)
            {
                case "first":
                    {
                        await giveFirst(e.Name);

                    }
                    break;
                case "resetfirst":
                    {
                        if (!_serviceBackbone.IsBroadcasterOrBot(e.Name)) return;
                        ResetFirst();
                    }
                    break;
            }
        }

        private void ResetFirst()
        {
            ClaimedFirst.Clear();
            CurrentClaims = 0;
        }

        private async Task giveFirst(string sender)
        {
            if (!_serviceBackbone.IsOnline)
            {
                await SendChatMessage(sender, "Nice try, the stream is currently offline.");
                return;
            }

            if (ClaimedFirst.Count >= MaxClaims)
            {
                await SendChatMessage(sender, "Sorry, You were to slow today. FeelsBadMan");
                return;
            }

            if (ClaimedFirst.Contains(sender.ToLower())) return;

            ClaimedFirst.Add(sender.ToLower());
            // var awardPoints = Tools.RandomRange(1, 25);
            // int awardPoints = 25;
            // if(CurrentClaims > 0)
            var awardPoints = (int)Math.Floor(((double)MaxClaims - CurrentClaims) / 2);
            if (awardPoints == 0)
            {
                await SendChatMessage(sender, "Sorry, You were to slow today. FeelsBadMan");
                return;
            }
            CurrentClaims++;
            _logger.LogInformation($"Current Claims: {CurrentClaims}");
            await _ticketsFeature.GiveTicketsToViewer(sender, awardPoints);
            await SendChatMessage(sender, string.Format("Whooohooo! You came in position {0} and get {1} tickets!! PogChamp", ClaimedFirst.Count, awardPoints));
        }
    }
}