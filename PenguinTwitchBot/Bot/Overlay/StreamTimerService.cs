using Microsoft.AspNetCore.SignalR;
using PenguinTwitchBot.Bot.Hubs;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;
using System.Text.Json;

namespace PenguinTwitchBot.Bot.Overlay
{
    /// <summary>
    /// Holds the state of the on-stream timer overlay. The timer is never started automatically —
    /// it only runs once a sub-action (or the UI) explicitly starts it, including after a restart.
    /// </summary>
    public class StreamTimerService(
        IHubContext<MainHub> hubContext,
        IServiceScopeFactory scopeFactory,
        ILogger<StreamTimerService> logger) : IStreamTimerService, IHostedService
    {
        public const string DirectionUp = "up";
        public const string DirectionDown = "down";

        private const string SettingName = "StreamTimerState";

        /// <summary>How often a running timer flushes its value, so a crash loses at most this much.</summary>
        private static readonly TimeSpan PersistInterval = TimeSpan.FromSeconds(15);

        private readonly Lock _lock = new();
        private readonly SemaphoreSlim _persistLock = new(1, 1);

        private bool _isRunning;
        private string _direction = DirectionUp;
        private double _anchorSeconds;
        private DateTime _anchorUtc = DateTime.UtcNow;

        private double _lastPersistedSeconds = -1;
        private string _lastPersistedDirection = "";
        private Timer? _persistTimer;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await RestoreAsync();

            _persistTimer = new Timer(state => _ = PersistTickAsync(), null, PersistInterval, PersistInterval);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_persistTimer != null)
            {
                await _persistTimer.DisposeAsync();
                _persistTimer = null;
            }

            await PersistAsync();
        }

        public StreamTimerState GetState()
        {
            lock (_lock)
            {
                return BuildState();
            }
        }

        public double GetCurrentSeconds()
        {
            lock (_lock)
            {
                return CurrentSeconds();
            }
        }

        public Task StartAsync(string? direction = null, double? startSeconds = null, bool reset = false)
        {
            lock (_lock)
            {
                Anchor();
                ApplyDirection(direction);

                if (startSeconds.HasValue)
                {
                    _anchorSeconds = Math.Max(0, startSeconds.Value);
                }
                else if (reset)
                {
                    _anchorSeconds = 0;
                }

                _isRunning = true;
            }

            return PublishAsync();
        }

        public Task StopAsync(bool reset = false)
        {
            lock (_lock)
            {
                Anchor();
                _isRunning = false;
                if (reset) _anchorSeconds = 0;
            }

            return PublishAsync();
        }

        public Task ConfigureAsync(string? direction = null, double? seconds = null)
        {
            lock (_lock)
            {
                Anchor();
                ApplyDirection(direction);

                if (seconds.HasValue)
                {
                    _anchorSeconds = Math.Max(0, seconds.Value);
                }
            }

            return PublishAsync();
        }

        public Task AddTimeAsync(double seconds) => AdjustAsync(seconds);

        public Task RemoveTimeAsync(double seconds) => AdjustAsync(-seconds);

        public Task SetTimeAsync(double seconds)
        {
            lock (_lock)
            {
                Anchor();
                _anchorSeconds = Math.Max(0, seconds);
            }

            return PublishAsync();
        }

        private Task AdjustAsync(double delta)
        {
            lock (_lock)
            {
                Anchor();
                _anchorSeconds = Math.Max(0, _anchorSeconds + delta);
            }

            return PublishAsync();
        }

        private void ApplyDirection(string? direction)
        {
            if (string.IsNullOrWhiteSpace(direction)) return;

            _direction = direction.Equals(DirectionDown, StringComparison.OrdinalIgnoreCase)
                ? DirectionDown
                : DirectionUp;
        }

        /// <summary>Collapses the elapsed running time into the anchor so mutations apply to the live value.</summary>
        private void Anchor()
        {
            _anchorSeconds = CurrentSeconds();
            _anchorUtc = DateTime.UtcNow;
        }

        private double CurrentSeconds()
        {
            if (!_isRunning) return _anchorSeconds;

            var elapsed = (DateTime.UtcNow - _anchorUtc).TotalSeconds;
            return _direction == DirectionDown
                ? Math.Max(0, _anchorSeconds - elapsed)
                : _anchorSeconds + elapsed;
        }

        private StreamTimerState BuildState()
            => new(_isRunning, _direction, _anchorSeconds, _anchorUtc, DateTime.UtcNow);

        private async Task PublishAsync()
        {
            await BroadcastAsync();
            await PersistAsync();
        }

        private async Task BroadcastAsync()
        {
            var state = GetState();
            try
            {
                await hubContext.Clients.All.SendAsync("StreamTimerUpdate", state);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed broadcasting stream timer state");
            }
        }

        private async Task PersistTickAsync()
        {
            bool isRunning;
            lock (_lock)
            {
                isRunning = _isRunning;
            }

            // A stopped timer cannot drift, and every explicit change already persisted itself.
            if (isRunning) await PersistAsync();
        }

        private async Task PersistAsync()
        {
            double seconds;
            string direction;
            lock (_lock)
            {
                seconds = CurrentSeconds();
                direction = _direction;
            }

            if (direction == _lastPersistedDirection && Math.Abs(seconds - _lastPersistedSeconds) < 0.5)
            {
                return;
            }

            await _persistLock.WaitAsync();
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var setting = await unitOfWork.Settings.Find(x => x.Name.Equals(SettingName)).FirstOrDefaultAsync();
                setting ??= new Setting { Name = SettingName, DataType = Setting.DataTypeEnum.String };
                setting.StringSetting = JsonSerializer.Serialize(new PersistedTimerState(direction, seconds));

                unitOfWork.Settings.Update(setting);
                await unitOfWork.SaveChangesAsync();

                _lastPersistedSeconds = seconds;
                _lastPersistedDirection = direction;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed persisting stream timer state");
            }
            finally
            {
                _persistLock.Release();
            }
        }

        private async Task RestoreAsync()
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var setting = await unitOfWork.Settings.Find(x => x.Name.Equals(SettingName)).FirstOrDefaultAsync();
                if (setting == null || string.IsNullOrWhiteSpace(setting.StringSetting)) return;

                var persisted = JsonSerializer.Deserialize<PersistedTimerState>(setting.StringSetting);
                if (persisted == null) return;

                lock (_lock)
                {
                    // Restored stopped on purpose; the timer must never start itself.
                    _isRunning = false;
                    _direction = persisted.Direction == DirectionDown ? DirectionDown : DirectionUp;
                    _anchorSeconds = double.IsFinite(persisted.Seconds) && persisted.Seconds > 0 ? persisted.Seconds : 0;
                    _anchorUtc = DateTime.UtcNow;

                    _lastPersistedDirection = _direction;
                    _lastPersistedSeconds = _anchorSeconds;
                }

                logger.LogInformation("Restored stream timer at {Seconds}s counting {Direction} (stopped)", _anchorSeconds, _direction);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed restoring stream timer state");
            }
        }

        private sealed record PersistedTimerState(string Direction, double Seconds);
    }
}
