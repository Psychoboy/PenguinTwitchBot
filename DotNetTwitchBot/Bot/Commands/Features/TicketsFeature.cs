using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models;
using DotNetTwitchBot.Repository;
using System.Timers;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public class TicketsFeature : BaseCommandService, ITicketsFeature, IHostedService
    {
        private readonly ILogger<TicketsFeature> _logger;

        readonly Timer _autoPointsTimer;

        private readonly IViewerFeature _viewerFeature;
        private readonly IPointsSystem _pointsSystem;
        private static readonly Prometheus.Gauge NumberOfTicketsGained = Prometheus.Metrics.CreateGauge("number_of_tickets_gained", "Number of Tickets gained since last stream start", labelNames: new[] { "viewer" });
        private readonly IServiceBackbone _serviceBackbone;
        private readonly IGameSettingsService _gameSettings;

        public TicketsFeature(
            ILogger<TicketsFeature> logger,
            IServiceBackbone serviceBackbone,
            IPointsSystem pointsSystem,
            IGameSettingsService gameSettingsService,
            IViewerFeature viewerFeature,
            ICommandHandler commandHandler)
            : base(serviceBackbone, commandHandler, "TicketsFeature")
        {
            _logger = logger;

            _autoPointsTimer = new Timer(300000); //5 minutes
            _autoPointsTimer.Elapsed += OnTimerElapsed;
            _viewerFeature = viewerFeature;
            _pointsSystem = pointsSystem;
            _autoPointsTimer.Start();
            _serviceBackbone = serviceBackbone;
            _serviceBackbone.StreamStarted += StreamStarted;
            _gameSettings = gameSettingsService;

        }

        public async Task<int> GetPointsForEveryone()
        {
            return await _gameSettings.GetIntSetting(ModuleName, "PointsForEveryone", 1);
        }

        public async Task SetPointsForEveryone(int points)
        {
            await _gameSettings.SetIntSetting(ModuleName, "PointsForEveryone", points);
        }

        public async Task<int> GetPointsForActiveUsers()
        {
            return await _gameSettings.GetIntSetting(ModuleName, "PointsForActiveUsers", 5);
        }

        public async Task SetPointsForActiveUsers(int points)
        {
            await _gameSettings.SetIntSetting(ModuleName, "PointsForActiveUsers", points);
        }

        public async Task<int> GetPointsForSubs()
        {
            return await _gameSettings.GetIntSetting(ModuleName, "PointsForSubs", 6);
        }

        public async Task SetPointsForSubs(int points)
        {
            await _gameSettings.SetIntSetting(ModuleName, "PointsForSubs", points);
        }

        private Task StreamStarted(object? sender, EventArgs _)
        {
            return Task.Run(() =>
            {
                var labels = NumberOfTicketsGained.GetAllLabelValues();
                foreach (var label in labels)
                {
                    NumberOfTicketsGained.RemoveLabelled(label);
                }
            });
        }

        public async Task GiveTicketsToActiveAndSubsOnlineWithBonus(long amount, long bonusAmount)
        {
            var activeViewers = _viewerFeature.GetActiveViewers();
            var onlineViewers = _viewerFeature.GetCurrentViewers();
            foreach (var viewer in onlineViewers)
            {
                if (activeViewers.Contains(viewer, StringComparer.OrdinalIgnoreCase) == false && await _viewerFeature.IsSubscriber(viewer))
                {
                    activeViewers.Add(viewer);
                }
            }
            await GiveTicketsWithBonusToViewers(activeViewers.Distinct(), amount, bonusAmount);
        }

        public async Task GiveTicketsToActiveUsers(long amount)
        {
            var activeViewers = _viewerFeature.GetActiveViewers();
            var onlineViewers = _viewerFeature.GetCurrentViewers();
            foreach (var viewer in onlineViewers)
            {
                if (activeViewers.Contains(viewer, StringComparer.OrdinalIgnoreCase) == false && await _viewerFeature.IsSubscriber(viewer))
                {
                    activeViewers.Add(viewer);
                }
            }
            await GiveTicketsWithBonusToViewers(activeViewers.Distinct(), amount, 0);
        }

        private async Task GiveTicketsToAllOnlineViewersWithBonus(long amount, long bonusAmount)
        {
            var viewers = _viewerFeature.GetCurrentViewers();
            await GiveTicketsWithBonusToViewers(viewers, amount, bonusAmount);
        }

        public async Task GiveTicketsWithBonusToViewers(IEnumerable<string> viewers, long amount, long subBonusAmount)
        {
            foreach (var viewer in viewers)
            {
                long bonus = 0;
                var viewerData = await _viewerFeature.GetViewerByUserName(viewer);
                if (viewerData != null && !string.IsNullOrWhiteSpace(viewerData.UserId))
                {
                    bonus = viewerData.isSub ? subBonusAmount : 0; // Sub Bonus
                    await GiveTicketsToViewerByUserId(viewerData.UserId, amount + bonus);
                }
                
            }
        }
        public async Task<long> GiveTicketsToViewerByUsername(string username, long amount)
        {
            var viewerData = await _viewerFeature.GetViewerByUserName(username);
            if (viewerData == null || string.IsNullOrWhiteSpace(viewerData.UserId))
            {
                return 0;
            }
            return await GiveTicketsToViewerByUserId(viewerData.UserId, amount);
        }

        public async Task<long> GiveTicketsToViewerByUserId(string userid, long amount)
        {
            var viewer = await _viewerFeature.GetViewerByUserId(userid);
            if(viewer == null)
            {
                _logger.LogWarning("Viewer for userid {userid} was null.", userid);
                return 0;
            }
            if (ServiceBackbone.IsKnownBot(viewer.Username)) return 0;
            await _pointsSystem.AddPointsByUserIdAndGame(userid, ModuleName, amount);
            if (amount > 0)
            {
                NumberOfTicketsGained.WithLabels(viewer.Username).Inc(amount);
            }

            return (await _pointsSystem.GetUserPointsByUserIdAndGame(userid, ModuleName)).Points;
        }

        private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (ServiceBackbone.IsOnline)
            {
                var pointsForEveryone = await GetPointsForEveryone();
                var pointsForActiveUsers = await GetPointsForActiveUsers();
                var pointsForSubs = await GetPointsForSubs();

                _logger.LogInformation("Starting to give  out tickets");
                await GiveTicketsToActiveAndSubsOnlineWithBonus(pointsForActiveUsers, pointsForSubs);
                await GiveTicketsToAllOnlineViewersWithBonus(pointsForEveryone, 0);
            }
        }


        public override async Task Register()
        {
            var moduleName = "TicketsFeature";
            await _pointsSystem.RegisterDefaultPointForGame(ModuleName);
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.CompletedTask;
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