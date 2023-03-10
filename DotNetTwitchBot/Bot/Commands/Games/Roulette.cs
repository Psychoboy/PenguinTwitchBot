using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands.Games
{
    public class Roulette : BaseCommand
    {
        private int MustBeatValue = 50;
        private TicketsFeature _ticketsFeature;
        public Roulette(
            ServiceBackbone eventService,
            TicketsFeature ticketsFeature
        ) : base(eventService)
        {
            _ticketsFeature = ticketsFeature;
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch (e.Command)
            {
                case "testroulette":
                    {
                        if (!IsCoolDownExpired(e.Name, e.Command)) return;
                        if (e.Args.Count == 0)
                        {
                            await SendChatMessage(e.DisplayName, "To roulette tickets please do !roulette Amount/All/Max replacing amount with how many you would like to risk.");
                            return;
                        }
                        var maxBet = false;
                        var amount = e.Args[0];
                        if (amount.Equals("all", StringComparison.CurrentCultureIgnoreCase) ||
                            amount.Equals("max", StringComparison.CurrentCultureIgnoreCase))
                        {
                            var viewerPoints = await _ticketsFeature.GetViewerTickets(e.Name);
                            if (viewerPoints > Int32.MaxValue / 2)
                            {
                                viewerPoints = (Int32.MaxValue - 1) / 2;
                            }
                            amount = viewerPoints.ToString();
                            maxBet = true;
                        }

                        var amountToBet = 0;
                        if (!Int32.TryParse(amount, out amountToBet))
                        {
                            await SendChatMessage(e.DisplayName, "The amount must be a number, max, or all");
                            return;
                        }

                        if (amountToBet <= 0)
                        {
                            await SendChatMessage(e.DisplayName, "The amount needs to be greater then 0");
                            return;
                        }

                        if (amountToBet > await _ticketsFeature.GetViewerTickets(e.Name))
                        {
                            await SendChatMessage(e.DisplayName, "You don't have that many tickets.");
                            return;
                        }

                        AddCoolDown(e.Name, e.Command, 30);
                        if (Tools.CurrentThreadRandom.Next(100) > MustBeatValue)
                        {
                            await _ticketsFeature.GiveTicketsToViewer(e.Name, amountToBet);
                            var totalPoints = await _ticketsFeature.GetViewerTickets(e.Name);
                            await SendChatMessage(
                            string.Format(maxBet ? AllInWinMessage : WinMessage, e.DisplayName, amountToBet, totalPoints));
                        }
                        else
                        {
                            await _ticketsFeature.RemoveTicketsFromViewer(e.Name, amountToBet);
                            var totalPoints = await _ticketsFeature.GetViewerTickets(e.Name);
                            await SendChatMessage(
                            string.Format(maxBet ? AllInLoseMessage : LoseMessage, e.DisplayName, amountToBet, totalPoints));
                        }
                    }
                    break;
            }
        }

        private string WinMessage = "{0} won {1:n0} tickets in the roulette and now has {2:n0} tickets! FeelsGoodMan";
        private string LoseMessage = "{0} lost {1:n0} tickets in the roulette and now has {2:n0} tickets! FeelsBadMan";
        private string AllInWinMessage = "PogChamp {0} went all in and won {1:n0} tickets PogChamp they now have {2:n0} tickets FeelsGoodMan";
        private string AllInLoseMessage = "{0} went all in and lost every single one of their {1:n0} tickets LUL";

    }
}