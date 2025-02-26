using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events.Chat;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Commands.TicketGames
{
    public class Roulette : BaseCommandService, IHostedService
    {
        private readonly int MustBeatValue = 52;
        private readonly ConcurrentDictionary<string, int> TotalGambled = new();
        private readonly int MaxAmount = 1000;
        private readonly int MaxPerBet = 500;
        private readonly ILogger<Roulette> _logger;
        private readonly IPointsSystem _pointsSystem;

        public Roulette(
            IServiceBackbone serviceBackbone,
            IPointsSystem pointsSystem,
            ICommandHandler commandHandler,
            ILogger<Roulette> logger
        ) : base(serviceBackbone, commandHandler, "Roulette")
        {
            _pointsSystem = pointsSystem;
            ServiceBackbone.StreamStarted += OnStreamStarted;
            _logger = logger;
        }

        private Task OnStreamStarted(object? sender, EventArgs _)
        {
            TotalGambled.Clear();
            return Task.CompletedTask;
        }

        public override async Task Register()
        {
            var moduleName = "Roulette";
            await RegisterDefaultCommand("roulette", this, moduleName, Rank.Viewer, userCooldown: 1200);
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "roulette":
                    {
                        if (ServiceBackbone.IsOnline == false) return;

                        if (e.Args.Count == 0)
                        {
                            await SendChatMessage(e.DisplayName, "To roulette tickets please do !roulette Amount/All/Max replacing amount with how many you would like to risk.");
                            throw new SkipCooldownException();
                        }
                        var amount = e.Args[0];
                        if (amount.Equals("all", StringComparison.CurrentCultureIgnoreCase) ||
                            amount.Equals("max", StringComparison.CurrentCultureIgnoreCase))
                        {
                            var viewerPoints = await _pointsSystem.GetUserPointsByUsernameAndGame(e.Name, ModuleName);
                            if (viewerPoints.Points > Int32.MaxValue / 2)
                            {
                                viewerPoints.Points = (Int32.MaxValue - 1) / 2;
                            }
                            amount = viewerPoints.ToString();
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

                        if (amountToBet > (await _pointsSystem.GetUserPointsByUsernameAndGame(e.Name, ModuleName)).Points)
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
                            //await _ticketsFeature.GiveTicketsToViewerByUserId(e.UserId, amountToBet);
                            var totalPoints = await _pointsSystem.AddPointsByUserIdAndGame(e.UserId, ModuleName, amountToBet);
                            //var totalPoints = await _ticketsFeature.GetViewerTickets(e.Name);
                            await SendChatMessage(
                            string.Format(WinMessage, e.DisplayName, amountToBet, totalPoints, value, TotalGambled[e.Name], MaxAmount));
                        }
                        else
                        {
                            //await _ticketsFeature.RemoveTicketsFromViewerByUserId(e.UserId, amountToBet);
                            var totalPoints = await _pointsSystem.RemovePointsFromUserByUserIdAndGame(e.UserId, ModuleName, amountToBet);
                            //var totalPoints = await _ticketsFeature.GetViewerTickets(e.Name);
                            await SendChatMessage(
                            string.Format(LoseMessage, e.DisplayName, amountToBet, totalPoints, value, TotalGambled[e.Name], MaxAmount));
                        }
                    }
                    break;
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Started {moduledname}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }

        private readonly string WinMessage = "{0} rolled a {3} and  won {1:n0} tickets in the roulette and now has {2:n0} tickets! FeelsGoodMan Rouletted {4} of {5} limit per stream";
        private readonly string LoseMessage = "{0} rolled a {3} and lost {1:n0} tickets in the roulette and now has {2:n0} tickets! FeelsBadMan Rouletted {4} of {5} limit per stream";
    }
}