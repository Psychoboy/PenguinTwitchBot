using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.TicketGames
{
    public class Roulette : BaseCommand
    {
        private int MustBeatValue = 52;
        private TicketsFeature _ticketsFeature;
        private ConcurrentDictionary<string, int> TotalGambled = new ConcurrentDictionary<string, int>();
        private int MaxAmount = 1000;
        private int MaxPerBet = 100;

        public Roulette(
            ServiceBackbone serviceBackbone,
            TicketsFeature ticketsFeature
        ) : base(serviceBackbone)
        {
            _ticketsFeature = ticketsFeature;
            _serviceBackbone.StreamEnded += OnStreamEnded;
        }

        private Task OnStreamEnded(object? sender)
        {
            TotalGambled.Clear();
            return Task.CompletedTask;
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch (e.Command)
            {
                case "roulette":
                    {
                        if (_serviceBackbone.IsOnline == false) return;
                        var isCoolDownExpired = await IsCoolDownExpiredWithMessage(e.Name, e.DisplayName, e.Command);
                        if (isCoolDownExpired == false) return;

                        if (e.Args.Count == 0)
                        {
                            await SendChatMessage(e.DisplayName, "To roulette tickets please do !roulette Amount/All/Max replacing amount with how many you would like to risk.");
                            return;
                        }
                        // var maxBet = false;
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
                            // maxBet = true;
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

                        if (amountToBet > MaxPerBet) amountToBet = MaxPerBet;

                        if (TotalGambled.ContainsKey(e.Name))
                        {
                            if (TotalGambled[e.Name] >= MaxAmount)
                            {
                                await SendChatMessage(e.DisplayName, $"You have reached your max per stream limit for !roulette ({MaxAmount} tickets).");
                                return;
                            }
                            if (TotalGambled[e.Name] + amountToBet > MaxAmount)
                            {
                                amountToBet = MaxAmount - TotalGambled[e.Name];
                            }
                        }
                        else
                        {
                            TotalGambled[e.Name] = 0;
                        }

                        AddCoolDown(e.Name, e.Command, DateTime.Now.AddMinutes(20));

                        TotalGambled[e.Name] += amountToBet;

                        var value = Tools.Next(100);
                        if (value > MustBeatValue)
                        {
                            await _ticketsFeature.GiveTicketsToViewer(e.Name, amountToBet);
                            var totalPoints = await _ticketsFeature.GetViewerTickets(e.Name);
                            await SendChatMessage(
                            string.Format(WinMessage, e.DisplayName, amountToBet, totalPoints, value, TotalGambled[e.Name], MaxAmount));
                        }
                        else
                        {
                            await _ticketsFeature.RemoveTicketsFromViewer(e.Name, amountToBet);
                            var totalPoints = await _ticketsFeature.GetViewerTickets(e.Name);
                            await SendChatMessage(
                            string.Format(LoseMessage, e.DisplayName, amountToBet, totalPoints, value, TotalGambled[e.Name], MaxAmount));
                        }
                    }
                    break;
            }
        }

        private string WinMessage = "rolled a {3} and  won {0} {1:n0} tickets in the roulette and now has {2:n0} tickets! FeelsGoodMan Rouletted {4} of {5} limit per stream";
        private string LoseMessage = "rolled a {3} and {0} lost {1:n0} tickets in the roulette and now has {2:n0} tickets! FeelsBadMan Rouletted {4} of {5} limit per stream";
        // private string AllInWinMessage = "PogChamp rolled {3} {0} went all in and won {1:n0} tickets PogChamp they now have {2:n0} tickets FeelsGoodMan Rouletted {4} of {5} limit per stream";
        // private string AllInLoseMessage = "rolled {3} and {0} went all in and lost every single one of their {1:n0} tickets LUL";

    }
}