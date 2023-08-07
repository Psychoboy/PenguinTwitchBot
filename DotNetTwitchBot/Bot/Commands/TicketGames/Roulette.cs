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
    public class Roulette : BaseCommandService
    {
        private readonly int MustBeatValue = 52;
        private readonly TicketsFeature _ticketsFeature;
        private readonly ConcurrentDictionary<string, int> TotalGambled = new();
        private readonly int MaxAmount = 1000;
        private readonly int MaxPerBet = 500;
        private readonly ILogger<Roulette> _logger;

        public Roulette(
            ServiceBackbone serviceBackbone,
            TicketsFeature ticketsFeature,
            IServiceScopeFactory scopeFactory,
            CommandHandler commandHandler,
            ILogger<Roulette> logger
        ) : base(serviceBackbone, scopeFactory, commandHandler)
        {
            _ticketsFeature = ticketsFeature;
            _serviceBackbone.StreamStarted += OnStreamStarted;
            _logger = logger;
        }

        private Task OnStreamStarted(object? sender)
        {
            TotalGambled.Clear();
            return Task.CompletedTask;
        }

        public override async Task Register()
        {
            var moduleName = "Roulette";
            await RegisterDefaultCommand("roulette", this, moduleName, Rank.Viewer, userCooldown: 1200);
            _logger.LogInformation($"Registered commands for {moduleName}");
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "roulette":
                    {
                        if (_serviceBackbone.IsOnline == false) return;

                        if (e.Args.Count == 0)
                        {
                            await SendChatMessage(e.DisplayName, "To roulette tickets please do !roulette Amount/All/Max replacing amount with how many you would like to risk.");
                            throw new SkipCooldownException();
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

                        if (!Int32.TryParse(amount, out int amountToBet))
                        {
                            await SendChatMessage(e.DisplayName, "The amount must be a number, max, or all");
                            throw new SkipCooldownException();
                        }

                        if (amountToBet <= 0)
                        {
                            await SendChatMessage(e.DisplayName, "The amount needs to be greater then 0");
                            throw new SkipCooldownException();
                        }

                        if (amountToBet > await _ticketsFeature.GetViewerTickets(e.Name))
                        {
                            await SendChatMessage(e.DisplayName, "You don't have that many tickets.");
                            throw new SkipCooldownException();
                        }

                        if (amountToBet > MaxPerBet) amountToBet = MaxPerBet;

                        if (TotalGambled.TryGetValue(e.Name, out var userTotalGambled))
                        {
                            if (userTotalGambled >= MaxAmount)
                            {
                                await SendChatMessage(e.DisplayName, $"You have reached your max per stream limit for !roulette ({MaxAmount} tickets).");
                                throw new SkipCooldownException();
                            }
                            if (userTotalGambled + amountToBet > MaxAmount)
                            {
                                amountToBet = MaxAmount - TotalGambled[e.Name];
                            }
                        }
                        else
                        {
                            TotalGambled[e.Name] = 0;
                        }

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

        private readonly string WinMessage = "rolled a {3} and  won {0} {1:n0} tickets in the roulette and now has {2:n0} tickets! FeelsGoodMan Rouletted {4} of {5} limit per stream";
        private readonly string LoseMessage = "rolled a {3} and {0} lost {1:n0} tickets in the roulette and now has {2:n0} tickets! FeelsBadMan Rouletted {4} of {5} limit per stream";
    }
}