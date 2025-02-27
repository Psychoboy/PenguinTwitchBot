using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events.Chat;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Commands.TicketGames
{
    public class Roulette : BaseCommandService, IHostedService
    {
        //private int MustBeatValue = 52;
        //private int MaxAmount = 1000;
        //private int MaxPerBet = 500;
        private readonly ConcurrentDictionary<string, int> TotalGambled = new();
        private readonly ILogger<Roulette> _logger;
        private readonly IGameSettingsService _gameSettings;
        private readonly IPointsSystem _pointsSystem;

        public const string GAMENAME = "Roulette";
        public const string NO_ARGS = "NoArgs";
        public const string BAD_ARGS = "BadArgs";
        public const string LESS_THAN_ZERO = "LessThanZero";
        public const string NOT_ENOUGH = "NotEnough";
        public const string WIN_MESSAGE = "WinMessage";
        public const string LOSE_MESSAGE = "LoseMessage";
        public const string REACHED_LIMIT = "ReachedLimit";
        public const string MUST_BEAT = "MustBeatValue";
        public const string MAX_AMOUNT = "MaxAmount";
        public const string MAX_PER_BET = "MaxPerBet";
        public const string ONLINE_ONLY = "OnlineOnly";

        public Roulette(
            IServiceBackbone serviceBackbone,
            IPointsSystem pointsSystem,
            IGameSettingsService gameSettingsService,
            ICommandHandler commandHandler,
            ILogger<Roulette> logger
        ) : base(serviceBackbone, commandHandler, GAMENAME)
        {
            _pointsSystem = pointsSystem;
            ServiceBackbone.StreamStarted += OnStreamStarted;
            _logger = logger;
            _gameSettings = gameSettingsService;
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
            await _pointsSystem.RegisterDefaultPointForGame(ModuleName);
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
                        var MaxAmount = await _gameSettings.GetIntSetting(
                            Roulette.GAMENAME,
                            Roulette.MAX_AMOUNT,
                            1000
                        );
                        var MustBeatValue = await _gameSettings.GetIntSetting(
                            GAMENAME,
                            MUST_BEAT,
                            52);
                        var MaxPerBet = await _gameSettings.GetIntSetting(
                            GAMENAME,
                            MAX_PER_BET,
                            500);
                        if (await _gameSettings.GetBoolSetting(GAMENAME, ONLINE_ONLY, true))
                        {
                            if (ServiceBackbone.IsOnline == false) return;
                        }

                        if (e.Args.Count == 0)
                        {
                            var noArgs = await _gameSettings.GetStringSetting(
                            Roulette.GAMENAME,
                            Roulette.NO_ARGS,
                            "To roulette please do !roulette Amount/All/Max replacing amount with how many you would like to risk.");
                            await SendChatMessage(e.DisplayName, noArgs);
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
                            var badArgs = await _gameSettings.GetStringSetting(
                                Roulette.GAMENAME,
                                Roulette.BAD_ARGS,
                                "The amount must be a number, max, or all"
                            );
                            await SendChatMessage(e.DisplayName, badArgs);
                            throw new SkipCooldownException();
                        }

                        if (amountToBet <= 0)
                        {
                            var lessThanZero = await _gameSettings.GetStringSetting(
                            Roulette.GAMENAME,
                            Roulette.LESS_THAN_ZERO,
                            "The amount needs to be greater then 0");
                            await SendChatMessage(e.DisplayName, lessThanZero);
                            throw new SkipCooldownException();
                        }

                        if (amountToBet > (await _pointsSystem.GetUserPointsByUsernameAndGame(e.Name, ModuleName)).Points)
                        {
                            var notEnough = await _gameSettings.GetStringSetting(
                                Roulette.GAMENAME,
                                Roulette.NOT_ENOUGH,
                                "You don't have that many."
                            );
                            await SendChatMessage(e.DisplayName, notEnough);
                            throw new SkipCooldownException();
                        }

                        if (amountToBet > MaxPerBet) amountToBet = MaxPerBet;
                        
                        var pointType = await _gameSettings.GetPointTypeForGame(GAMENAME);
                        if (TotalGambled.TryGetValue(e.Name, out var userTotalGambled))
                        {
                            if (userTotalGambled >= MaxAmount)
                            {
                                var reachedLimit = await _gameSettings.GetStringSetting(
                                    Roulette.GAMENAME,
                                    Roulette.REACHED_LIMIT,
                                    "You have reached your max per stream limit for !roulette ({MaxAmount} {PointsName})."
                                );
                                reachedLimit = reachedLimit
                                    .Replace("{MaxAmount}", MaxAmount.ToString("N0"))
                                    .Replace("{PointsName}", pointType.Name);
                                await SendChatMessage(e.DisplayName, reachedLimit);
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
                            var totalPoints = await _pointsSystem.AddPointsByUserIdAndGame(e.UserId, ModuleName, amountToBet);
                            var winMessage = await _gameSettings.GetStringSetting(
                                GAMENAME,
                                WIN_MESSAGE,
                                "{Name} rolled a {Rolled} and won {WonPoints} {PointsName} in the roulette and now has {TotalPoints} {PointsName}! FeelsGoodMan Rouletted {TotalBet} of {MaxBet} limit per stream"
                                );
                            winMessage = winMessage
                                .Replace("{Name}", e.DisplayName)
                                .Replace("{Rolled}", value.ToString("N0"))
                                .Replace("{WonPoints}", amountToBet.ToString("N0"))
                                .Replace("{PointsName}", pointType.Name)
                                .Replace("{TotalPoints}", totalPoints.ToString("N0"))
                                .Replace("{TotalBet}", TotalGambled[e.Name].ToString("N0"))
                                .Replace("{MaxBet}", MaxAmount.ToString("N0"));
                            await SendChatMessage(winMessage);
                        }
                        else
                        {
                            await _pointsSystem.RemovePointsFromUserByUserIdAndGame(e.UserId, ModuleName, amountToBet);
                            var totalPoints = await _pointsSystem.GetUserPointsByUserIdAndGame(e.UserId, ModuleName);
                            var loseMessage = await _gameSettings.GetStringSetting(
                                GAMENAME,
                                LOSE_MESSAGE,
                                "{Name} rolled a {Rolled} and lost {WonPoints} {PointsName} in the roulette and now has {TotalPoints} {PointsName}! FeelsGoodMan Rouletted {TotalBet} of {MaxBet} limit per stream"
                                );
                            loseMessage = loseMessage
                                .Replace("{Name}", e.DisplayName)
                                .Replace("{Rolled}", value.ToString("N0"))
                                .Replace("{WonPoints}", amountToBet.ToString("N0"))
                                .Replace("{PointsName}", pointType.Name)
                                .Replace("{TotalPoints}", totalPoints.Points.ToString("N0"))
                                .Replace("{TotalBet}", TotalGambled[e.Name].ToString("N0"))
                                .Replace("{MaxBet}", MaxAmount.ToString("N0"));
                            await SendChatMessage(loseMessage);
                        }
                    }
                    break;
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Started {moduledname}", ModuleName);
            
            await  Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }

        //private readonly string WinMessage = "{0} rolled a {3} and  won {1:n0} tickets in the roulette and now has {2:n0} tickets! FeelsGoodMan Rouletted {4} of {5} limit per stream";
        //private readonly string LoseMessage = "{0} rolled a {3} and lost {1:n0} tickets in the roulette and now has {2:n0} tickets! FeelsBadMan Rouletted {4} of {5} limit per stream";
    }
}