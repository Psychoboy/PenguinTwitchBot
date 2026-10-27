using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Database.Bot.Models.Points;
using System.Collections.Concurrent;
using System.Timers;
using Timer = System.Timers.Timer;

namespace PenguinTwitchBot.Bot.Core.Points
{
    public class PassivePointsService : IPassivePointsService, IHostedService, IDisposable
    {
        private readonly ILogger<PassivePointsService> _logger;
        private readonly IServiceBackbone _serviceBackbone;
        private readonly IPointsSystem _pointsSystem;
        private readonly IViewerFeature _viewerFeature;
        private readonly IFeatureRuntimeCoordinator _featureRuntimeCoordinator;
        private readonly TimeProvider _timeProvider;
        private readonly Timer _tickTimer = new(TimeSpan.FromMinutes(1).TotalMilliseconds);
        private readonly SemaphoreSlim _distributionLock = new(1, 1);
        private readonly ConcurrentDictionary<Guid, DateTime> _lastAwardedUtc = new();

        public PassivePointsService(
            ILogger<PassivePointsService> logger,
            IServiceBackbone serviceBackbone,
            IPointsSystem pointsSystem,
            IViewerFeature viewerFeature,
            IFeatureRuntimeCoordinator featureRuntimeCoordinator,
            TimeProvider? timeProvider = null)
        {
            _logger = logger;
            _serviceBackbone = serviceBackbone;
            _pointsSystem = pointsSystem;
            _viewerFeature = viewerFeature;
            _featureRuntimeCoordinator = featureRuntimeCoordinator;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        private bool IsFeatureEnabled() => _featureRuntimeCoordinator.IsEnabled(FeatureKeys.PassivePoints);

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting {Module}", nameof(PassivePointsService));
            _lastAwardedUtc.Clear();
            _tickTimer.Elapsed += OnTimerElapsed;
            _tickTimer.Start();
            _serviceBackbone.StreamStarted += OnStreamStarted;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopped {Module}", nameof(PassivePointsService));
            _tickTimer.Stop();
            _tickTimer.Elapsed -= OnTimerElapsed;
            _serviceBackbone.StreamStarted -= OnStreamStarted;
            return Task.CompletedTask;
        }

        public void ResetScheduleForPointType(Guid pointTypeId)
        {
            if (pointTypeId == Guid.Empty) return;
            _lastAwardedUtc[pointTypeId] = _timeProvider.GetUtcNow().UtcDateTime;
        }

        private Task OnStreamStarted(object? sender, EventArgs _)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            foreach (var key in _lastAwardedUtc.Keys.ToList())
            {
                _lastAwardedUtc[key] = now;
            }
            return Task.CompletedTask;
        }

        private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                await ProcessTickAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception during passive points timer tick");
            }
        }

        public async Task ProcessTickAsync(CancellationToken cancellationToken = default)
        {
            if (!IsFeatureEnabled()) return;
            if (!_serviceBackbone.IsOnline)
            {
                _lastAwardedUtc.Clear();
                return;
            }

            if (!_distributionLock.Wait(0))
            {
                _logger.LogWarning("Passive points distribution already in progress; skipping tick.");
                return;
            }

            try
            {
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var pointTypes = await _pointsSystem.GetPointTypes();

                foreach (var pointType in pointTypes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var pointTypeId = pointType.GetId();
                    if (pointTypeId == Guid.Empty) continue;

                    if (!pointType.PassivePointsEnabled ||
                        (pointType.PassivePointsAllViewers <= 0 &&
                         pointType.PassivePointsActiveViewers <= 0 &&
                         pointType.PassivePointsSubscribers <= 0))
                    {
                        _lastAwardedUtc.TryRemove(pointTypeId, out _);
                        continue;
                    }

                    var intervalMinutes = Math.Max(1, pointType.PassivePointsIntervalMinutes);
                    if (!_lastAwardedUtc.TryGetValue(pointTypeId, out var lastAwarded))
                    {
                        _lastAwardedUtc[pointTypeId] = now;
                        continue;
                    }

                    if (now - lastAwarded >= TimeSpan.FromMinutes(intervalMinutes))
                    {
                        _lastAwardedUtc[pointTypeId] = now;
                        await DistributeForPointTypeAsync(pointType, cancellationToken);
                    }
                }
            }
            finally
            {
                _distributionLock.Release();
            }
        }

        public async Task DistributeForPointTypeAsync(PointType pointType, CancellationToken cancellationToken = default)
        {
            var pointTypeId = pointType.GetId();
            if (pointTypeId == Guid.Empty) return;

            var allAmount = Math.Max(0, pointType.PassivePointsAllViewers);
            var activeAmount = Math.Max(0, pointType.PassivePointsActiveViewers);
            var subAmount = Math.Max(0, pointType.PassivePointsSubscribers);

            if (allAmount == 0 && activeAmount == 0 && subAmount == 0)
            {
                return;
            }

            var currentViewers = _viewerFeature.GetCurrentViewers()
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var activeViewers = _viewerFeature.GetActiveViewers()
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var viewerName in currentViewers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_serviceBackbone.IsKnownBot(viewerName))
                {
                    continue;
                }

                try
                {
                    var isSub = await _viewerFeature.IsSubscriber(viewerName);
                    // Subscribers in chat are always considered active viewers
                    var isActive = isSub || activeViewers.Contains(viewerName);

                    long totalPoints = allAmount;
                    if (isActive && activeAmount > 0)
                    {
                        totalPoints += activeAmount;
                    }
                    if (isSub && subAmount > 0)
                    {
                        totalPoints += subAmount;
                    }

                    if (totalPoints <= 0)
                    {
                        continue;
                    }

                    var userId = await _viewerFeature.GetViewerId(viewerName);
                    if (string.IsNullOrWhiteSpace(userId))
                    {
                        continue;
                    }

                    await _pointsSystem.AddPointsByUserId(userId, pointTypeId, totalPoints);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error awarding passive points ({PointType}) to viewer {Viewer}", pointType.Name, viewerName);
                }
            }
        }

        public void Dispose()
        {
            _tickTimer.Dispose();
            _distributionLock.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

