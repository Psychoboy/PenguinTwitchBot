using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Bot.Commands.TicketGames
{
    public class First : BaseCommandService, IHostedService
    {
        private List<string> ClaimedFirst { get; } = new List<string>();
        private readonly Lock _claimLock = new();
        private readonly IPointsSystem _pointsSystem;
        private readonly ILogger<First> _logger;
        private readonly IGameSettingsService _gameSettingsService;

        public First(
            IServiceBackbone eventService,
            ILogger<First> logger,
            IPointsSystem pointsSystem,
            Application.Notifications.IPenguinDispatcher dispatcher,
            ICommandHandler commandHandler,
            IGameSettingsService gameSettingsService
        ) : base(eventService, commandHandler, "First", dispatcher)
        {
            _pointsSystem = pointsSystem;
            _logger = logger;
            _gameSettingsService = gameSettingsService;
        }

        public static int CurrentClaims { get; set; } = 0;

        public override async Task Register()
        {
            var moduleName = "First";
            await RegisterDefaultCommand("first", this, moduleName);
            await RegisterDefaultCommand("resetfirst", this, moduleName, Rank.Streamer);
            await _pointsSystem.RegisterDefaultPointForGame(ModuleName);
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommandDefaultName(e.Command);
            switch (command)
            {
                case "first":
                    {
                        await GiveFirst(e.Name);

                    }
                    break;
                case "resetfirst":
                    {
                        ResetFirst();
                    }
                    break;
            }
        }

        private void ResetFirst()
        {
            lock (_claimLock)
            {
                ClaimedFirst.Clear();
                CurrentClaims = 0;
            }
        }

        private async Task GiveFirst(string sender)
        {
            var options = await FirstGameOptions.LoadAsync(_gameSettingsService);
            if (!ServiceBackbone.IsOnline)
            {
                await SendChatMessage(sender, options.OfflineMessage);
                throw new SkipCooldownException();
            }

            long awardPoints;
            int position;
            bool tooSlow = false;
            bool alreadyClaimed = false;

            lock (_claimLock)
            {
                if (ClaimedFirst.Count >= options.MaximumClaims)
                {
                    tooSlow = true;
                    awardPoints = 0;
                    position = 0;
                }
                else if (ClaimedFirst.Contains(sender, StringComparer.OrdinalIgnoreCase))
                {
                    alreadyClaimed = true;
                    awardPoints = 0;
                    position = 0;
                }
                else
                {
                    awardPoints = options.GetAwardForPriorClaims(ClaimedFirst.Count);
                    if (awardPoints == 0)
                    {
                        tooSlow = true;
                        position = 0;
                    }
                    else
                    {
                        ClaimedFirst.Add(sender);
                        position = ClaimedFirst.Count;
                        CurrentClaims = position;
                    }
                }
            }

            if (alreadyClaimed) throw new SkipCooldownException();
            if (tooSlow)
            {
                await SendChatMessage(sender, options.TooSlowMessage);
                throw new SkipCooldownException();
            }

            _logger.LogInformation("Current Claims: {CurrentClaims}", position);
            await _pointsSystem.AddPointsByUsernameAndGame(sender, ModuleName, awardPoints);
            var pointType = await _pointsSystem.GetPointTypeForGame(ModuleName);
            var message = options.AwardMessage
                .Replace("{Position}", position.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{Amount}", awardPoints.ToString("N0"), StringComparison.OrdinalIgnoreCase)
                .Replace("{PointType}", pointType.Name, StringComparison.OrdinalIgnoreCase);
            await SendChatMessage(sender, message);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting {moduledname}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}
