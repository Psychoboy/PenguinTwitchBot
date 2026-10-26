using Microsoft.AspNetCore.SignalR;
using PenguinTwitchBot.Bot.Hubs;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;
using System.Text.Json;

namespace PenguinTwitchBot.Bot.Overlay
{
    /// <summary>
    /// Holds the state of on-stream timer overlays. Timers are never started automatically —
    /// they only run once a sub-action (or the UI) explicitly starts them, including after a restart.
    /// Supports multiple timers; the primary/backwards-compatible timer is named "Default".
    /// </summary>
    public class StreamTimerService(
        IHubContext<MainHub> hubContext,
        IServiceScopeFactory scopeFactory,
        ILogger<StreamTimerService> logger) : IStreamTimerService, IHostedService
    {
        public const string DefaultTimerName = "Default";
        public const string DirectionUp = "up";
        public const string DirectionDown = "down";

        private const string LegacySettingName = "StreamTimerState";
        private const string TimersSettingName = "StreamTimers";

        /// <summary>How often a running timer flushes its value, so a crash loses at most this much.</summary>
        private static readonly TimeSpan PersistInterval = TimeSpan.FromSeconds(15);

        private readonly Lock _lock = new();
        private readonly SemaphoreSlim _persistLock = new(1, 1);

        private readonly Dictionary<string, TimerEntry> _timers = new(StringComparer.OrdinalIgnoreCase)
        {
            [DefaultTimerName] = new TimerEntry(DefaultTimerName)
        };

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

        public IReadOnlyList<string> GetTimerNames()
        {
            lock (_lock)
            {
                var names = _timers.Keys
                    .Where(k => !k.Equals(DefaultTimerName, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                names.Insert(0, DefaultTimerName);
                return names;
            }
        }

        public IReadOnlyList<StreamTimerState> GetAllStates()
        {
            lock (_lock)
            {
                return _timers.Values.Select(t => t.BuildState()).ToList();
            }
        }

        public StreamTimerState GetState(string? timerName = null)
        {
            lock (_lock)
            {
                return GetOrCreateTimerLocked(timerName).BuildState();
            }
        }

        public double GetCurrentSeconds(string? timerName = null)
        {
            lock (_lock)
            {
                return GetOrCreateTimerLocked(timerName).CurrentSeconds();
            }
        }

        public Task<bool> CreateTimerAsync(string timerName)
        {
            var name = NormalizeName(timerName);
            TimerEntry entry;
            lock (_lock)
            {
                if (_timers.ContainsKey(name))
                    return Task.FromResult(false);

                entry = new TimerEntry(name);
                _timers[name] = entry;
            }

            return PublishAndPersistCreatedAsync(entry);
        }

        private async Task<bool> PublishAndPersistCreatedAsync(TimerEntry entry)
        {
            await PublishAsync(entry);
            return true;
        }

        public async Task<bool> DeleteTimerAsync(string timerName)
        {
            var name = NormalizeName(timerName);
            if (name.Equals(DefaultTimerName, StringComparison.OrdinalIgnoreCase))
                return false;

            TimerEntry? removed;
            lock (_lock)
            {
                if (!_timers.Remove(name, out removed))
                    return false;
            }

            // Broadcast final stopped/zeroed state so overlays know it stopped
            removed.IsRunning = false;
            removed.AnchorSeconds = 0;
            removed.AnchorUtc = DateTime.UtcNow;
            await BroadcastAsync(removed.BuildState());
            await PersistAsync(force: true);
            return true;
        }

        public Task StartTimerAsync(string timerName, string? direction = null, double? startSeconds = null, bool reset = false)
        {
            TimerEntry entry;
            lock (_lock)
            {
                entry = GetOrCreateTimerLocked(timerName);
                entry.Anchor();
                entry.ApplyDirection(direction);

                if (startSeconds.HasValue)
                {
                    entry.AnchorSeconds = Math.Max(0, startSeconds.Value);
                }
                else if (reset)
                {
                    entry.AnchorSeconds = 0;
                }

                entry.IsRunning = true;
            }

            return PublishAsync(entry);
        }

        public Task StopTimerAsync(string timerName, bool reset = false)
        {
            TimerEntry entry;
            lock (_lock)
            {
                entry = GetOrCreateTimerLocked(timerName);
                entry.Anchor();
                entry.IsRunning = false;
                if (reset) entry.AnchorSeconds = 0;
            }

            return PublishAsync(entry);
        }

        public Task ConfigureTimerAsync(string timerName, string? direction = null, double? seconds = null)
        {
            TimerEntry entry;
            lock (_lock)
            {
                entry = GetOrCreateTimerLocked(timerName);
                entry.Anchor();
                entry.ApplyDirection(direction);

                if (seconds.HasValue)
                {
                    entry.AnchorSeconds = Math.Max(0, seconds.Value);
                }
            }

            return PublishAsync(entry);
        }

        public Task AddTimeAsync(string timerName, double seconds) => AdjustAsync(timerName, seconds);

        public Task RemoveTimeAsync(string timerName, double seconds) => AdjustAsync(timerName, -seconds);

        public Task SetTimeAsync(string timerName, double seconds)
        {
            TimerEntry entry;
            lock (_lock)
            {
                entry = GetOrCreateTimerLocked(timerName);
                entry.Anchor();
                entry.AnchorSeconds = Math.Max(0, seconds);
            }

            return PublishAsync(entry);
        }

        // Backwards compatibility default timer methods
        public Task StartAsync(string? direction = null, double? startSeconds = null, bool reset = false)
            => StartTimerAsync(DefaultTimerName, direction, startSeconds, reset);

        public Task StopAsync(bool reset = false)
            => StopTimerAsync(DefaultTimerName, reset);

        public Task ConfigureAsync(string? direction = null, double? seconds = null)
            => ConfigureTimerAsync(DefaultTimerName, direction, seconds);

        public Task AddTimeAsync(double seconds) => AddTimeAsync(DefaultTimerName, seconds);

        public Task RemoveTimeAsync(double seconds) => RemoveTimeAsync(DefaultTimerName, seconds);

        public Task SetTimeAsync(double seconds) => SetTimeAsync(DefaultTimerName, seconds);

        private Task AdjustAsync(string timerName, double delta)
        {
            TimerEntry entry;
            lock (_lock)
            {
                entry = GetOrCreateTimerLocked(timerName);
                entry.Anchor();
                entry.AnchorSeconds = Math.Max(0, entry.AnchorSeconds + delta);
            }

            return PublishAsync(entry);
        }

        private static string NormalizeName(string? name)
        {
            return string.IsNullOrWhiteSpace(name) ? DefaultTimerName : name.Trim();
        }

        private TimerEntry GetOrCreateTimerLocked(string? name)
        {
            var normalized = NormalizeName(name);
            if (!_timers.TryGetValue(normalized, out var entry))
            {
                entry = new TimerEntry(normalized);
                _timers[normalized] = entry;
            }
            return entry;
        }

        private async Task PublishAsync(TimerEntry entry)
        {
            await BroadcastAsync(entry.BuildState());
            await PersistAsync(force: true);
        }

        private async Task BroadcastAsync(StreamTimerState state)
        {
            try
            {
                await hubContext.Clients.All.SendAsync("StreamTimerUpdate", state);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed broadcasting stream timer state for {Timer}", state.Name);
            }
        }

        private async Task PersistTickAsync()
        {
            bool anyRunning;
            lock (_lock)
            {
                anyRunning = _timers.Values.Any(t => t.IsRunning);
            }

            if (anyRunning) await PersistAsync();
        }

        private async Task PersistAsync(bool force = false)
        {
            List<(TimerEntry Entry, double Seconds, string Direction)> snapshots;
            lock (_lock)
            {
                snapshots = _timers.Values.Select(t => (t, t.CurrentSeconds(), t.Direction)).ToList();
            }

            if (!force)
            {
                var anyChanged = snapshots.Any(s =>
                    s.Direction != s.Entry.LastPersistedDirection ||
                    Math.Abs(s.Seconds - s.Entry.LastPersistedSeconds) >= 0.5);

                if (!anyChanged) return;
            }

            await _persistLock.WaitAsync();
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                // Build full dictionary of persisted states
                var dict = new Dictionary<string, PersistedTimerState>(StringComparer.OrdinalIgnoreCase);
                PersistedTimerState? defaultState = null;

                foreach (var (entry, seconds, direction) in snapshots)
                {
                    var persisted = new PersistedTimerState(direction, seconds);
                    dict[entry.Name] = persisted;
                    if (entry.Name.Equals(DefaultTimerName, StringComparison.OrdinalIgnoreCase))
                    {
                        defaultState = persisted;
                    }

                    entry.LastPersistedSeconds = seconds;
                    entry.LastPersistedDirection = direction;
                }

                // 1. Save all timers under TimersSettingName
                var allTimersSetting = await unitOfWork.Settings.Find(x => x.Name.Equals(TimersSettingName)).FirstOrDefaultAsync();
                allTimersSetting ??= new Setting { Name = TimersSettingName, DataType = Setting.DataTypeEnum.String };
                allTimersSetting.StringSetting = JsonSerializer.Serialize(dict);
                unitOfWork.Settings.Update(allTimersSetting);

                // 2. Also save Default timer under LegacySettingName for backwards compatibility
                if (defaultState != null)
                {
                    var legacySetting = await unitOfWork.Settings.Find(x => x.Name.Equals(LegacySettingName)).FirstOrDefaultAsync();
                    legacySetting ??= new Setting { Name = LegacySettingName, DataType = Setting.DataTypeEnum.String };
                    legacySetting.StringSetting = JsonSerializer.Serialize(defaultState);
                    unitOfWork.Settings.Update(legacySetting);
                }

                await unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed persisting stream timer states");
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

                Dictionary<string, PersistedTimerState>? restoredTimers = null;

                var allTimersSetting = await unitOfWork.Settings.Find(x => x.Name.Equals(TimersSettingName)).FirstOrDefaultAsync();
                if (allTimersSetting != null && !string.IsNullOrWhiteSpace(allTimersSetting.StringSetting))
                {
                    try
                    {
                        restoredTimers = JsonSerializer.Deserialize<Dictionary<string, PersistedTimerState>>(allTimersSetting.StringSetting);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed deserializing stream timers dictionary");
                    }
                }

                var legacySetting = await unitOfWork.Settings.Find(x => x.Name.Equals(LegacySettingName)).FirstOrDefaultAsync();
                PersistedTimerState? legacyState = null;
                if (legacySetting != null && !string.IsNullOrWhiteSpace(legacySetting.StringSetting))
                {
                    try
                    {
                        legacyState = JsonSerializer.Deserialize<PersistedTimerState>(legacySetting.StringSetting);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed deserializing legacy stream timer state");
                    }
                }

                lock (_lock)
                {
                    _timers.Clear();

                    if (restoredTimers != null)
                    {
                        foreach (var kvp in restoredTimers)
                        {
                            var entry = new TimerEntry(kvp.Key)
                            {
                                IsRunning = false,
                                Direction = kvp.Value.Direction == DirectionDown ? DirectionDown : DirectionUp,
                                AnchorSeconds = double.IsFinite(kvp.Value.Seconds) && kvp.Value.Seconds > 0 ? kvp.Value.Seconds : 0,
                                AnchorUtc = DateTime.UtcNow
                            };
                            entry.LastPersistedDirection = entry.Direction;
                            entry.LastPersistedSeconds = entry.AnchorSeconds;
                            _timers[kvp.Key] = entry;
                        }
                    }

                    // If Default wasn't in restoredTimers, populate from legacyState or defaults
                    if (!_timers.TryGetValue(DefaultTimerName, out var defaultEntry))
                    {
                        defaultEntry = new TimerEntry(DefaultTimerName);
                        if (legacyState != null)
                        {
                            defaultEntry.Direction = legacyState.Direction == DirectionDown ? DirectionDown : DirectionUp;
                            defaultEntry.AnchorSeconds = double.IsFinite(legacyState.Seconds) && legacyState.Seconds > 0 ? legacyState.Seconds : 0;
                        }
                        defaultEntry.LastPersistedDirection = defaultEntry.Direction;
                        defaultEntry.LastPersistedSeconds = defaultEntry.AnchorSeconds;
                        _timers[DefaultTimerName] = defaultEntry;
                    }
                }

                logger.LogInformation("Restored {Count} stream timer(s)", _timers.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed restoring stream timer states");
            }
        }

        private sealed class TimerEntry(string name)
        {
            public string Name { get; } = name;
            public bool IsRunning { get; set; }
            public string Direction { get; set; } = DirectionUp;
            public double AnchorSeconds { get; set; }
            public DateTime AnchorUtc { get; set; } = DateTime.UtcNow;

            public double LastPersistedSeconds { get; set; } = -1;
            public string LastPersistedDirection { get; set; } = "";

            public void ApplyDirection(string? direction)
            {
                if (string.IsNullOrWhiteSpace(direction)) return;
                Direction = direction.Equals(DirectionDown, StringComparison.OrdinalIgnoreCase)
                    ? DirectionDown
                    : DirectionUp;
            }

            public void Anchor()
            {
                AnchorSeconds = CurrentSeconds();
                AnchorUtc = DateTime.UtcNow;
            }

            public double CurrentSeconds()
            {
                if (!IsRunning) return AnchorSeconds;

                var elapsed = (DateTime.UtcNow - AnchorUtc).TotalSeconds;
                return Direction == DirectionDown
                    ? Math.Max(0, AnchorSeconds - elapsed)
                    : AnchorSeconds + elapsed;
            }

            public StreamTimerState BuildState()
                => new(IsRunning, Direction, AnchorSeconds, AnchorUtc, DateTime.UtcNow, Name);
        }

        private sealed record PersistedTimerState(string Direction, double Seconds);
    }
}
