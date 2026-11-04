using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Database.Bot.Models.Points;
using PenguinTwitchBot.Database.Repository;
using System.Timers;
using Timer = System.Timers.Timer;

namespace PenguinTwitchBot.Bot.Commands.PastyGames
{
    public class Tax : BaseCommandService, IHostedService
    {
        readonly Timer _taxTimer;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<Tax> _logger;
        private readonly IPointsSystem _pointSystem;
        private readonly IGameSettingsService _gameSettingsService;
        private readonly SemaphoreSlim _processing = new(1, 1);

        public Tax(
            ILogger<Tax> logger,
            IServiceScopeFactory scopeFactory,
            IServiceBackbone serviceBackbone,
            ICommandHandler commandHandler,
            Application.Notifications.IPenguinDispatcher dispatcher,
            IPointsSystem pointsSystem,
            IGameSettingsService gameSettingsService
            ) : base(serviceBackbone, commandHandler, "Tax", dispatcher)
        {
            _taxTimer = new Timer(TimeSpan.FromMinutes(30).TotalMilliseconds);
            _taxTimer.Elapsed += Elapsed;
            _scopeFactory = scopeFactory;
            ServiceBackbone.StreamEnded += OnStreamEnded;
            ServiceBackbone.StreamStarted += OnStreamStarted;
            _logger = logger;
            _pointSystem = pointsSystem;
            _gameSettingsService = gameSettingsService;
        }

        private Task OnStreamStarted(object? sender, EventArgs _)
        {
            _taxTimer.Stop();
            return Task.CompletedTask;
        }

        private Task OnStreamEnded(object? sender, EventArgs _)
        {
            _taxTimer.Start();
            return Task.CompletedTask;
        }

        private async void Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (ServiceBackbone.IsOnline || !await _processing.WaitAsync(0))
            {
                return;
            }

            try
            {
                _taxTimer.Stop();
                _logger.LogInformation("Processing Tax");
                var now = DateTime.UtcNow;
                var policies = (await TaxPointTypePolicySettings.LoadAsync(_pointSystem, _gameSettingsService))
                    .Where(entry => entry.Policy.Enabled)
                    .ToList();
                if (policies.Count == 0)
                {
                    return;
                }

                var oldestCutoff = now.AddDays(-policies.Max(entry => entry.Policy.InactiveDays));
                List<Viewer> viewers;
                Dictionary<Guid, string> pointTypeNames;
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    viewers = await db.Viewers.Find(x => x.LastSeen < oldestCutoff).ToListAsync();
                    pointTypeNames = (await db.PointTypes.GetAllAsync()).ToDictionary(type => type.Id, type => type.Name);
                }

                var inactiveViewers = viewers.ToDictionary(viewer => viewer.UserId, viewer => viewer.LastSeen, StringComparer.Ordinal);
                foreach (var entry in policies)
                {
                    long totalRemoved = 0;
                    var policy = entry.Policy;
                    var cutoff = now.AddDays(-policy.InactiveDays);
                    List<UserPoints> balances;
                    await using (var scope = _scopeFactory.CreateAsyncScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                        balances = await db.UserPoints.Find(points =>
                            points.PointTypeId == policy.PointTypeId && points.Points > policy.MinimumBalance).ToListAsync();
                    }

                    foreach (var balance in balances)
                    {
                        if (!inactiveViewers.TryGetValue(balance.UserId, out var lastSeen) || lastSeen >= cutoff)
                        {
                            continue;
                        }

                        var deduction = TaxPointTypePolicySettings.CalculateDeduction(balance.Points, policy);
                        if (deduction == 0)
                        {
                            continue;
                        }

                        try
                        {
                            if (await _pointSystem.RemovePointsFromUserByUserId(balance.UserId, policy.PointTypeId, deduction))
                            {
                                totalRemoved += deduction;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Tax deduction failed for viewer {UserId} and point type {PointTypeId}", balance.UserId, policy.PointTypeId);
                        }
                    }

                    var pointTypeName = pointTypeNames.GetValueOrDefault(policy.PointTypeId, policy.PointTypeId.ToString());
                    _logger.LogInformation("Removed {totalRemoved} {pointType} via taxes", totalRemoved, pointTypeName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running taxes");
            }
            finally
            {
                _processing.Release();
            }
        }
        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.CompletedTask;
        }

        public override Task Register()
        {
            return _pointSystem.RegisterDefaultPointForGame(ModuleName);
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
