using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Notifications;
using System.Text.Json;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public class FishingFrenzyService : IFishingFrenzyService, IDisposable
    {
        private const int MaxTrackedEndedTournaments = 256;

        private readonly object _lock = new();
        private readonly SemaphoreSlim _transitionLock = new(1, 1);
        private readonly HashSet<Guid> _endedTournamentIds = new();
        private readonly Queue<Guid> _endedTournamentOrder = new();
        private readonly IWebSocketMessenger _webSocketMessenger;
        private readonly IServiceBackbone _serviceBackbone;
        private readonly IFishingService _fishingService;
        private readonly IFishingAudioResolver _audioResolver;
        private readonly ILogger<FishingFrenzyService> _logger;
        private readonly IFishingMessageSettingsService? _messageSettingsService;

        private FishingFrenzyState? _activeState;
        private CancellationTokenSource? _expiryCts;
        private bool _disposed;

        public FishingFrenzyService(
            IWebSocketMessenger webSocketMessenger,
            IServiceBackbone serviceBackbone,
            IFishingService fishingService,
            IFishingAudioResolver audioResolver,
            ILogger<FishingFrenzyService> logger,
            IFishingMessageSettingsService? messageSettingsService = null)
        {
            _webSocketMessenger = webSocketMessenger;
            _serviceBackbone = serviceBackbone;
            _fishingService = fishingService;
            _audioResolver = audioResolver;
            _logger = logger;
            _messageSettingsService = messageSettingsService;
        }

        public FishingFrenzyState? GetActiveFrenzy()
        {
            lock (_lock)
            {
                if (_activeState != null && !_activeState.IsActiveAt(DateTime.UtcNow))
                {
                    // Return null without clearing _activeState so the expiry timer can complete
                    // its identity check and send the inactive broadcast / end announcement.
                    return null;
                }
                return _activeState;
            }
        }

        public async Task<FishingFrenzyState> StartFrenzyAsync(
            string name,
            int durationMinutes,
            double rarityMultiplier = 2.0,
            string? targetCategory = null,
            double categoryBoost = 0.5,
            bool announceInChat = true,
            Guid? linkedTournamentId = null)
        {
            if (!double.IsFinite(rarityMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(rarityMultiplier), "Rarity multiplier must be a finite number.");
            }
            if (!double.IsFinite(categoryBoost))
            {
                throw new ArgumentOutOfRangeException(nameof(categoryBoost), "Category boost must be a finite number.");
            }

            var safeName = string.IsNullOrWhiteSpace(name) ? "Fishing Frenzy" : name.Trim();
            var safeMinutes = Math.Clamp(durationMinutes, 1, 240);
            var safeMult = Math.Clamp(rarityMultiplier, 1.0, 10.0);
            var safeCategory = string.IsNullOrWhiteSpace(targetCategory) ? null : targetCategory.Trim();
            var safeCatBoost = safeCategory == null ? 0.0 : Math.Clamp(categoryBoost, 0.0, 0.85);

            // Load settings & audio before mutating active state so failures leave any previous Frenzy untouched.
            var settings = await _fishingService.GetSettings();
            var soundUrl = _audioResolver.ResolveFrenzySoundUrl(settings);

            await _transitionLock.WaitAsync();
            try
            {
                if (_disposed)
                {
                    return new FishingFrenzyState
                    {
                        Name = safeName,
                        RarityMultiplier = safeMult,
                        TargetCategory = safeCategory,
                        CategoryBoost = safeCatBoost,
                        StartedAtUtc = DateTime.UtcNow,
                        EndsAtUtc = DateTime.UtcNow,
                        LinkedTournamentId = linkedTournamentId
                    };
                }

                // If the paired tournament already ended or was deleted while launch was in flight, do not activate.
                if (linkedTournamentId.HasValue && _endedTournamentIds.Contains(linkedTournamentId.Value))
                {
                    _logger.LogInformation("Skipping paired Frenzy '{Name}' because tournament {TournamentId} already ended",
                        safeName, linkedTournamentId.Value);
                    var expiredNow = DateTime.UtcNow;
                    return new FishingFrenzyState
                    {
                        Name = safeName,
                        RarityMultiplier = safeMult,
                        TargetCategory = safeCategory,
                        CategoryBoost = safeCatBoost,
                        StartedAtUtc = expiredNow,
                        EndsAtUtc = expiredNow,
                        LinkedTournamentId = linkedTournamentId
                    };
                }

                var now = DateTime.UtcNow;
                var state = new FishingFrenzyState
                {
                    Name = safeName,
                    RarityMultiplier = safeMult,
                    TargetCategory = safeCategory,
                    CategoryBoost = safeCatBoost,
                    StartedAtUtc = now,
                    EndsAtUtc = now.AddMinutes(safeMinutes),
                    LinkedTournamentId = linkedTournamentId
                };

                CancellationToken expiryToken;
                lock (_lock)
                {
                    _expiryCts?.Cancel();
                    _expiryCts?.Dispose();
                    _expiryCts = new CancellationTokenSource();
                    expiryToken = _expiryCts.Token;
                    _activeState = state;
                }

                _ = ScheduleAutoExpiryAsync(state, announceInChat, expiryToken);

                await BroadcastFrenzyStateAsync(state, settings?.SoundEnabled ?? true, settings?.SoundVolume ?? 0.8, soundUrl);

                if (announceInChat)
                {
                    var bonusText = BuildBonusSummary(state);
                    try
                    {
                        var msgs = _messageSettingsService != null ? await _messageSettingsService.GetAsync() : new FishingMessageSettings();
                        var formatted = FishingMessageSettings.Format(msgs.FrenzyStart,
                            ("name", state.Name),
                            ("minutes", safeMinutes.ToString()),
                            ("bonus", bonusText),
                            ("multiplier", $"{state.RarityMultiplier:0.##}x"),
                            ("category", state.TargetCategory ?? "All Fish"));
                        if (!string.IsNullOrWhiteSpace(formatted))
                        {
                            await _serviceBackbone.SendChatMessage(formatted);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send Fishing Frenzy start announcement to chat");
                    }
                }

                _logger.LogInformation("Started Fishing Frenzy '{Name}' for {Minutes}m ({Mult}x, category={Category})",
                    state.Name, safeMinutes, safeMult, safeCategory ?? "All");

                return state;
            }
            finally
            {
                _transitionLock.Release();
            }
        }

        public async Task<bool> StopFrenzyAsync(bool announceInChat = true)
        {
            await _transitionLock.WaitAsync();
            try
            {
                FishingFrenzyState? previous;
                lock (_lock)
                {
                    previous = _activeState;
                    _activeState = null;
                    _expiryCts?.Cancel();
                    _expiryCts?.Dispose();
                    _expiryCts = null;
                }

                if (previous == null)
                {
                    return false;
                }

                await BroadcastFrenzyStateAsync(null, false, 0, null);

                if (announceInChat)
                {
                    try
                    {
                        var msgs = _messageSettingsService != null ? await _messageSettingsService.GetAsync() : new FishingMessageSettings();
                        var formatted = FishingMessageSettings.Format(msgs.FrenzyEnd, ("name", previous.Name));
                        if (!string.IsNullOrWhiteSpace(formatted))
                        {
                            await _serviceBackbone.SendChatMessage(formatted);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send Fishing Frenzy end announcement to chat");
                    }
                }

                return true;
            }
            finally
            {
                _transitionLock.Release();
            }
        }

        public async Task<bool> StopFrenzyForTournamentAsync(Guid tournamentId, bool announceInChat = false)
        {
            await _transitionLock.WaitAsync();
            try
            {
                RecordEndedTournamentLocked(tournamentId);

                FishingFrenzyState? previous;
                lock (_lock)
                {
                    if (_activeState == null || _activeState.LinkedTournamentId != tournamentId)
                    {
                        return false;
                    }

                    previous = _activeState;
                    _activeState = null;
                    _expiryCts?.Cancel();
                    _expiryCts?.Dispose();
                    _expiryCts = null;
                }

                await BroadcastFrenzyStateAsync(null, false, 0, null);

                if (announceInChat)
                {
                    try
                    {
                        var msgs = _messageSettingsService != null ? await _messageSettingsService.GetAsync() : new FishingMessageSettings();
                        var formatted = FishingMessageSettings.Format(msgs.FrenzyEnd, ("name", previous.Name));
                        if (!string.IsNullOrWhiteSpace(formatted))
                        {
                            await _serviceBackbone.SendChatMessage(formatted);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send Fishing Frenzy end announcement to chat");
                    }
                }

                return true;
            }
            finally
            {
                _transitionLock.Release();
            }
        }

        private void RecordEndedTournamentLocked(Guid tournamentId)
        {
            if (!_endedTournamentIds.Add(tournamentId))
            {
                return;
            }

            _endedTournamentOrder.Enqueue(tournamentId);
            while (_endedTournamentOrder.Count > MaxTrackedEndedTournaments)
            {
                var oldest = _endedTournamentOrder.Dequeue();
                _endedTournamentIds.Remove(oldest);
            }
        }

        private async Task ScheduleAutoExpiryAsync(FishingFrenzyState state, bool announceInChat, CancellationToken ct)
        {
            try
            {
                var delay = state.GetRemaining(DateTime.UtcNow);
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, ct);
                }

                if (ct.IsCancellationRequested)
                {
                    return;
                }

                await _transitionLock.WaitAsync(ct);
                try
                {
                    bool expiredCurrent;
                    lock (_lock)
                    {
                        expiredCurrent = ReferenceEquals(_activeState, state);
                        if (expiredCurrent)
                        {
                            _activeState = null;
                        }
                    }

                    if (expiredCurrent)
                    {
                        await BroadcastFrenzyStateAsync(null, false, 0, null);
                        if (announceInChat)
                        {
                            try
                            {
                                var msgs = _messageSettingsService != null ? await _messageSettingsService.GetAsync() : new FishingMessageSettings();
                                var formatted = FishingMessageSettings.Format(msgs.FrenzyEnd, ("name", state.Name));
                                if (!string.IsNullOrWhiteSpace(formatted))
                                {
                                    await _serviceBackbone.SendChatMessage(formatted);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to send Fishing Frenzy expiry announcement");
                            }
                        }
                    }
                }
                finally
                {
                    _transitionLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // Replaced or stopped manually
            }
            catch (ObjectDisposedException)
            {
                // Service disposed during shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Fishing Frenzy auto-expiry");
            }
        }

        private async Task BroadcastFrenzyStateAsync(FishingFrenzyState? state, bool soundEnabled, double soundVolume, string? soundUrl)
        {
            try
            {
                var payload = state == null
                    ? new
                    {
                        fishingFrenzy = new
                        {
                            active = false,
                            name = "",
                            bonusSummary = "",
                            startedAtUtc = (DateTime?)null,
                            endsAtUtc = (DateTime?)null,
                            soundEnabled = false,
                            soundVolume = 0.0,
                            soundUrl = ""
                        }
                    }
                    : new
                    {
                        fishingFrenzy = new
                        {
                            active = true,
                            name = state.Name,
                            bonusSummary = BuildBonusSummary(state),
                            startedAtUtc = (DateTime?)state.StartedAtUtc,
                            endsAtUtc = (DateTime?)state.EndsAtUtc,
                            soundEnabled,
                            soundVolume,
                            soundUrl = soundUrl ?? ""
                        }
                    };

                await _webSocketMessenger.AddToQueue(WsTopics.Fishing, JsonSerializer.Serialize(payload));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast Fishing Frenzy state via WebSocket");
            }
        }

        public static string BuildBonusSummary(FishingFrenzyState state)
        {
            var parts = new List<string>();
            if (state.RarityMultiplier > 1.0)
            {
                parts.Add($"{state.RarityMultiplier:0.##}x Rarity");
            }
            if (!string.IsNullOrWhiteSpace(state.TargetCategory) && state.CategoryBoost > 0)
            {
                parts.Add($"+{Math.Round(state.CategoryBoost * 100)}% {state.TargetCategory}");
            }
            return parts.Count > 0 ? string.Join(" • ", parts) : "Bonus Active";
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _disposed = true;
                _expiryCts?.Cancel();
                _expiryCts?.Dispose();
                _expiryCts = null;
            }
            _transitionLock.Dispose();
        }
    }
}
