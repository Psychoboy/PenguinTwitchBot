using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Notifications;
using System.Text.Json;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public class FishingFrenzyService : IFishingFrenzyService, IDisposable
    {
        private readonly object _lock = new();
        private readonly IWebSocketMessenger _webSocketMessenger;
        private readonly IServiceBackbone _serviceBackbone;
        private readonly IFishingService _fishingService;
        private readonly IFishingAudioResolver _audioResolver;
        private readonly ILogger<FishingFrenzyService> _logger;
        private readonly IFishingMessageSettingsService? _messageSettingsService;

        private FishingFrenzyState? _activeState;
        private CancellationTokenSource? _expiryCts;

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
                    _activeState = null;
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
            bool announceInChat = true)
        {
            var safeName = string.IsNullOrWhiteSpace(name) ? "Fishing Frenzy" : name.Trim();
            var safeMinutes = Math.Clamp(durationMinutes, 1, 180);
            var safeMult = Math.Clamp(rarityMultiplier, 1.0, 10.0);
            var safeCategory = string.IsNullOrWhiteSpace(targetCategory) ? null : targetCategory.Trim();
            var safeCatBoost = safeCategory == null ? 0.0 : Math.Clamp(categoryBoost, 0.0, 0.85);

            var now = DateTime.UtcNow;
            var state = new FishingFrenzyState
            {
                Name = safeName,
                RarityMultiplier = safeMult,
                TargetCategory = safeCategory,
                CategoryBoost = safeCatBoost,
                StartedAtUtc = now,
                EndsAtUtc = now.AddMinutes(safeMinutes)
            };

            CancellationTokenSource cts;
            lock (_lock)
            {
                _expiryCts?.Cancel();
                _expiryCts?.Dispose();
                _expiryCts = new CancellationTokenSource();
                cts = _expiryCts;
                _activeState = state;
            }

            _ = ScheduleAutoExpiryAsync(state, announceInChat, cts.Token);

            var settings = await _fishingService.GetSettings();
            var soundUrl = _audioResolver.ResolveFrenzySoundUrl(settings);

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

        public async Task<bool> StopFrenzyAsync(bool announceInChat = true)
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
            catch (TaskCanceledException)
            {
                // Replaced or stopped manually
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
                _expiryCts?.Cancel();
                _expiryCts?.Dispose();
                _expiryCts = null;
            }
        }
    }
}
