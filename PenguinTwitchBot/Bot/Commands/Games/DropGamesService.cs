using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Services;

namespace PenguinTwitchBot.Bot.Commands.Games
{
    public class DropGamesService(
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        IDefaultCommandTriggerService defaultCommandTriggerService,
        IServiceScopeFactory scopeFactory,
        IEmoteService emoteService,
        IWebSocketMessenger webSocketMessenger,
        Application.Notifications.IPenguinDispatcher dispatcher,
        ILogger<DropGamesService> logger)
        : BaseCommandService(serviceBackbone, commandHandler, "DropGames", dispatcher), IHostedService, IDropGamesService
    {
        public const string SettingsKey = "drop_games.settings.v1";
        private const double TickRateHz = 60.0;
        private const double Dt = 1.0 / TickRateHz;
        private const double ParachuteRadius = 0.036;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        private readonly ConcurrentDictionary<string, PendingDropRecord> _pendingDrops = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<DropGameType, DropGameSessionState> _sessions = new();
        private readonly object _simulationLock = new();
        private DropGamesSettings? _cachedSettings;

        private sealed class DropGameSessionState
        {
            public DropGameType GameType { get; init; }
            public DateTime StartedAtUtc { get; init; }
            public DateTime EndsAtUtc { get; set; }
            public int TotalDurationSeconds { get; set; }
            public string? ThemeIdOverride { get; set; }
            public string? StartedBy { get; init; }
            public ConcurrentDictionary<string, int> UserDropCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
            public int TotalDrops;
            public double? TargetCenterX { get; set; }
            public double? TargetHalfWidth { get; set; }
            public double? BullseyeHalfWidth { get; set; }
            public CancellationTokenSource Cts { get; } = new();
            public Timer? ExpiryTimer { get; set; }
        }


        public async Task<DropGamesSettings> GetSettingsAsync()
        {
            if (_cachedSettings != null)
            {
                return _cachedSettings;
            }

            DropGamesSettings? loaded = null;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetService<IUnitOfWork>();
                if (unitOfWork != null)
                {
                    var setting = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingsKey)).FirstOrDefault();
                    if (setting != null && !string.IsNullOrWhiteSpace(setting.StringSetting))
                    {
                        loaded = JsonSerializer.Deserialize<DropGamesSettings>(setting.StringSetting, _jsonOptions);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load DropGamesSettings from database; using defaults.");
            }

            if (loaded == null)
            {
                loaded = new DropGamesSettings();
                EnsureStockThemes(loaded);
                await PersistSettingsToDbAsync(loaded);
            }
            else
            {
                EnsureStockThemes(loaded);
            }

            _cachedSettings = loaded;
            return _cachedSettings;
        }

        public async Task SaveSettingsAsync(DropGamesSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            EnsureStockThemes(settings);
            _cachedSettings = settings;
            await PersistSettingsToDbAsync(settings);
        }

        private async Task PersistSettingsToDbAsync(DropGamesSettings settings)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetService<IUnitOfWork>();
                if (unitOfWork == null)
                {
                    return;
                }

                var json = JsonSerializer.Serialize(settings, _jsonOptions);
                var existing = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingsKey)).FirstOrDefault();
                if (existing == null)
                {
                    await unitOfWork.Settings.AddAsync(new Setting
                    {
                        Name = SettingsKey,
                        DataType = Setting.DataTypeEnum.String,
                        StringSetting = json
                    });
                }
                else
                {
                    existing.DataType = Setting.DataTypeEnum.String;
                    existing.StringSetting = json;
                    unitOfWork.Settings.Update(existing);
                }

                await unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to save DropGamesSettings to database.");
            }
        }

        private static void EnsureStockThemes(DropGamesSettings settings)
        {
            settings.Themes ??= [];
            if (settings.Themes.Count > 0)
            {
                // Deduplicate by Id, keeping the last entry in case previous deserialization appended to defaults
                settings.Themes = settings.Themes
                    .GroupBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.Last())
                    .ToList();
            }

            var stockDefaults = DropGameTheme.CreateDefaultSeasonalThemes();
            foreach (var stock in stockDefaults)
            {
                if (!settings.Themes.Any(t => t.Id.Equals(stock.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    settings.Themes.Add(stock);
                }
            }

            if (settings.Plinko.Buckets == null || settings.Plinko.Buckets.Count == 0)
            {
                settings.Plinko.Buckets = PlinkoGameSettings.CreateDefaultBuckets();
            }
            else if (settings.Plinko.Buckets.Count >= 14 &&
                     settings.Plinko.Buckets.Count % 7 == 0 &&
                     settings.Plinko.Buckets[0].Label == "MISS" &&
                     settings.Plinko.Buckets[1].Label == "50 PTS" &&
                     settings.Plinko.Buckets[1].IsWin &&
                     settings.Plinko.Buckets[3].Label == "JACKPOT")
            {
                // Clean up any prepended default 7 buckets from earlier collection initializer deserialization
                settings.Plinko.Buckets = settings.Plinko.Buckets.Skip(settings.Plinko.Buckets.Count - 7).ToList();
            }
        }

        public PendingDropRecord? GetPendingDrop(string dropToken)
        {
            if (string.IsNullOrWhiteSpace(dropToken)) return null;
            return _pendingDrops.TryGetValue(dropToken, out var record) ? record : null;
        }

        public DropGameSessionDto? GetCurrentSession(DropGameType gameType)
        {
            if (!_sessions.TryGetValue(gameType, out var state)) return null;
            var now = DateTime.UtcNow;
            if (state.EndsAtUtc <= now) return null;

            var remainingSec = Math.Max(0, (int)Math.Ceiling((state.EndsAtUtc - now).TotalSeconds));
            return new DropGameSessionDto
            {
                GameType = gameType.ToString().ToLowerInvariant(),
                IsActive = true,
                TotalDurationSeconds = state.TotalDurationSeconds,
                RemainingSeconds = remainingSec,
                StartedAtMs = new DateTimeOffset(state.StartedAtUtc).ToUnixTimeMilliseconds(),
                EndsAtMs = new DateTimeOffset(state.EndsAtUtc).ToUnixTimeMilliseconds(),
                TotalDrops = Volatile.Read(ref state.TotalDrops),
                TargetCenterX = state.TargetCenterX,
                TargetHalfWidth = state.TargetHalfWidth,
                BullseyeHalfWidth = state.BullseyeHalfWidth
            };
        }

        public async Task<DropGameSessionDto> StartSessionAsync(
            DropGameType gameType,
            int? durationSeconds = null,
            string? themeOverrideId = null,
            string? triggeredBy = null)
        {
            var settings = await GetSettingsAsync();
            var duration = durationSeconds.HasValue && durationSeconds.Value > 0
                ? durationSeconds.Value
                : (gameType == DropGameType.Parachute
                    ? settings.Parachute.DefaultSessionDurationSeconds
                    : settings.Plinko.DefaultSessionDurationSeconds);

            duration = Math.Clamp(duration, 5, 3600);

            // If an active session exists, stop it first cleanly
            await StopSessionAsync(gameType, triggeredBy: "restart");

            var now = DateTime.UtcNow;
            var endsAt = now.AddSeconds(duration);

            var state = new DropGameSessionState
            {
                GameType = gameType,
                StartedAtUtc = now,
                EndsAtUtc = endsAt,
                TotalDurationSeconds = duration,
                ThemeIdOverride = themeOverrideId,
                StartedBy = triggeredBy
            };

            // Set up auto-expiration timer bound specifically to this state instance
            state.ExpiryTimer = new Timer(async _ =>
            {
                try
                {
                    await StopSessionAsync(gameType, state, triggeredBy: "timer_expired");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error expiring session for {GameType}", gameType);
                }
            }, null, TimeSpan.FromSeconds(duration), Timeout.InfiniteTimeSpan);

            // Atomically register session, ensuring we never blindly overwrite another session without stopping it
            while (!_sessions.TryAdd(gameType, state))
            {
                if (_sessions.TryGetValue(gameType, out var existing))
                {
                    await StopSessionAsync(gameType, existing, triggeredBy: "restart");
                }
            }

            var theme = ResolveTheme(settings, themeOverrideId, gameType == DropGameType.Parachute ? settings.Parachute.ActiveThemeId : settings.Plinko.ActiveThemeId);

            if (gameType == DropGameType.Parachute)
            {
                var targetHalfWidth = Math.Clamp(settings.Parachute.TargetWidthPercent / 200.0, 0.02, 0.35);
                var bullseyeHalfWidth = Math.Min(targetHalfWidth, Math.Clamp(settings.Parachute.BullseyeWidthPercent / 200.0, 0.008, 0.15));

                // Safe margin so platform with padding and flags is 100% visible on screen
                var minCenterX = Math.Max(0.12, targetHalfWidth + 0.03);
                var maxCenterX = Math.Min(0.88, 1.0 - targetHalfWidth - 0.03);
                if (minCenterX >= maxCenterX)
                {
                    minCenterX = 0.20;
                    maxCenterX = 0.80;
                }

                state.TargetCenterX = Math.Round(RandomDouble(minCenterX, maxCenterX), 4);
                state.TargetHalfWidth = targetHalfWidth;
                state.BullseyeHalfWidth = bullseyeHalfWidth;
            }

            var dto = new DropGameSessionDto
            {
                GameType = gameType.ToString().ToLowerInvariant(),
                IsActive = true,
                TotalDurationSeconds = duration,
                RemainingSeconds = duration,
                StartedAtMs = new DateTimeOffset(now).ToUnixTimeMilliseconds(),
                EndsAtMs = new DateTimeOffset(endsAt).ToUnixTimeMilliseconds(),
                TotalDrops = 0,
                Theme = theme,
                TargetCenterX = state.TargetCenterX,
                TargetHalfWidth = state.TargetHalfWidth,
                BullseyeHalfWidth = state.BullseyeHalfWidth
            };

            var topic = gameType == DropGameType.Parachute ? WsTopics.Parachute : WsTopics.Plinko;
            var wsPayload = JsonSerializer.Serialize(new
            {
                type = "session_start",
                session = dto
            }, _jsonOptions);
            await QueueWsMessageAsync(topic, wsPayload, $"{gameType} session_start");

            // Chat Announcement
            var announce = gameType == DropGameType.Parachute
                ? settings.Parachute.AnnounceSessionStatusInChat
                : settings.Plinko.AnnounceSessionStatusInChat;
            if (announce)
            {
                var template = gameType == DropGameType.Parachute
                    ? settings.Parachute.SessionStartChatMessageTemplate
                    : settings.Plinko.SessionStartChatMessageTemplate;
                if (!string.IsNullOrWhiteSpace(template))
                {
                    var msg = template.Replace("{duration}", duration.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
                    await ServiceBackbone.SendChatMessage(msg);
                }
            }

            logger.LogInformation("Started {GameType} session for {Duration}s (Triggered by {User})", gameType, duration, triggeredBy ?? "System");
            return dto;
        }

        public async Task<DropGameSessionDto?> StopSessionAsync(
            DropGameType gameType,
            string? triggeredBy = null)
        {
            if (!_sessions.TryRemove(gameType, out var state))
            {
                return null;
            }

            try
            {
                state.ExpiryTimer?.Dispose();
                state.Cts.Cancel();
                state.Cts.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error disposing session timer/cts for {GameType}", gameType);
            }

            var totalDrops = Volatile.Read(ref state.TotalDrops);
            var settings = await GetSettingsAsync();
            var theme = ResolveTheme(settings, state.ThemeIdOverride, gameType == DropGameType.Parachute ? settings.Parachute.ActiveThemeId : settings.Plinko.ActiveThemeId);

            var dto = new DropGameSessionDto
            {
                GameType = gameType.ToString().ToLowerInvariant(),
                IsActive = false,
                TotalDurationSeconds = state.TotalDurationSeconds,
                RemainingSeconds = 0,
                StartedAtMs = new DateTimeOffset(state.StartedAtUtc).ToUnixTimeMilliseconds(),
                EndsAtMs = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds(),
                TotalDrops = totalDrops,
                Theme = theme,
                TargetCenterX = state.TargetCenterX,
                TargetHalfWidth = state.TargetHalfWidth,
                BullseyeHalfWidth = state.BullseyeHalfWidth
            };

            var topic = gameType == DropGameType.Parachute ? WsTopics.Parachute : WsTopics.Plinko;
            var wsPayload = JsonSerializer.Serialize(new
            {
                type = "session_end",
                session = dto
            }, _jsonOptions);
            await QueueWsMessageAsync(topic, wsPayload, $"{gameType} session_end");

            // Chat Announcement
            if (!string.Equals(triggeredBy, "restart", StringComparison.OrdinalIgnoreCase))
            {
                var announce = gameType == DropGameType.Parachute
                    ? settings.Parachute.AnnounceSessionStatusInChat
                    : settings.Plinko.AnnounceSessionStatusInChat;
                if (announce)
                {
                    var template = gameType == DropGameType.Parachute
                        ? settings.Parachute.SessionEndChatMessageTemplate
                        : settings.Plinko.SessionEndChatMessageTemplate;
                    if (!string.IsNullOrWhiteSpace(template))
                    {
                        var msg = template
                            .Replace("{count}", totalDrops.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                            .Replace("{duration}", state.TotalDurationSeconds.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
                        await ServiceBackbone.SendChatMessage(msg);
                    }
                }
            }

            logger.LogInformation("Stopped {GameType} session with {Drops} total drops (Triggered by {User})", gameType, totalDrops, triggeredBy ?? "System");
            return dto;
        }

        private async Task<DropGameSessionDto?> StopSessionAsync(
            DropGameType gameType,
            DropGameSessionState expectedState,
            string? triggeredBy = null)
        {
            ArgumentNullException.ThrowIfNull(expectedState);

            var kvp = new KeyValuePair<DropGameType, DropGameSessionState>(gameType, expectedState);
            var collection = (ICollection<KeyValuePair<DropGameType, DropGameSessionState>>)_sessions;
            if (!collection.Remove(kvp))
            {
                return null;
            }

            try
            {
                expectedState.ExpiryTimer?.Dispose();
                expectedState.Cts.Cancel();
                expectedState.Cts.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error disposing session timer/cts for {GameType}", gameType);
            }

            var totalDrops = Volatile.Read(ref expectedState.TotalDrops);
            var settings = await GetSettingsAsync();
            var theme = ResolveTheme(settings, expectedState.ThemeIdOverride, gameType == DropGameType.Parachute ? settings.Parachute.ActiveThemeId : settings.Plinko.ActiveThemeId);

            var dto = new DropGameSessionDto
            {
                GameType = gameType.ToString().ToLowerInvariant(),
                IsActive = false,
                TotalDurationSeconds = expectedState.TotalDurationSeconds,
                RemainingSeconds = 0,
                StartedAtMs = new DateTimeOffset(expectedState.StartedAtUtc).ToUnixTimeMilliseconds(),
                EndsAtMs = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds(),
                TotalDrops = totalDrops,
                Theme = theme,
                TargetCenterX = expectedState.TargetCenterX,
                TargetHalfWidth = expectedState.TargetHalfWidth,
                BullseyeHalfWidth = expectedState.BullseyeHalfWidth
            };

            var topic = gameType == DropGameType.Parachute ? WsTopics.Parachute : WsTopics.Plinko;
            var wsPayload = JsonSerializer.Serialize(new
            {
                type = "session_end",
                session = dto
            }, _jsonOptions);
            await QueueWsMessageAsync(topic, wsPayload, $"{gameType} session_end");

            // Chat Announcement
            if (!string.Equals(triggeredBy, "restart", StringComparison.OrdinalIgnoreCase))
            {
                var announce = gameType == DropGameType.Parachute
                    ? settings.Parachute.AnnounceSessionStatusInChat
                    : settings.Plinko.AnnounceSessionStatusInChat;
                if (announce)
                {
                    var template = gameType == DropGameType.Parachute
                        ? settings.Parachute.SessionEndChatMessageTemplate
                        : settings.Plinko.SessionEndChatMessageTemplate;
                    if (!string.IsNullOrWhiteSpace(template))
                    {
                        var msg = template
                            .Replace("{count}", totalDrops.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                            .Replace("{duration}", expectedState.TotalDurationSeconds.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
                        await ServiceBackbone.SendChatMessage(msg);
                    }
                }
            }

            logger.LogInformation("Stopped {GameType} session with {Drops} total drops (Triggered by {User})", gameType, totalDrops, triggeredBy ?? "System");
            return dto;
        }


        private void CleanupExpiredDrops()
        {
            var now = DateTime.UtcNow;
            foreach (var kvp in _pendingDrops)
            {
                if (kvp.Value.ExpiresAtUtc <= now)
                {
                    _pendingDrops.TryRemove(kvp.Key, out _);
                }
            }
        }

        public async Task<string?> ResolveCharacterOrEmoteUrlAsync(CommandEventArgs? eventArgs, string? emoteOrImageInput, string fallbackUrl, bool allowDirectUrlOrPath = false)
        {
            // 1. Native Twitch emote fragment attached to CommandEventArgs
            if (!string.IsNullOrWhiteSpace(eventArgs?.EmoteUrl))
            {
                return eventArgs.EmoteUrl;
            }

            var candidate = !string.IsNullOrWhiteSpace(emoteOrImageInput)
                ? emoteOrImageInput.Trim()
                : eventArgs?.Arg?.Trim();

            if (!string.IsNullOrWhiteSpace(candidate))
            {
                // Direct URL or local media path (only allowed when explicitly trusted, e.g. subaction configuration or dashboard test)
                if (allowDirectUrlOrPath &&
                    (candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                     candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                     candidate.StartsWith('/')))
                {
                    return candidate;
                }

                // First token lookup in EmoteService (Twitch + BTTV + FFZ + 7TV)
                var firstWord = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstWord) && !firstWord.StartsWith('%'))
                {
                    try
                    {
                        var emotes = await emoteService.GetEmotesAsync();
                        var match = emotes.FirstOrDefault(e => e.Key.Equals(firstWord, StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrWhiteSpace(match.Value))
                        {
                            return match.Value;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Emote lookup failed for token {Token}", firstWord);
                    }
                }
            }

            // 3. Fallback to theme's default character/droplet image URL (empty string uses built-in seasonal sprite on overlay)
            return fallbackUrl;
        }

        private static DropGameTheme ResolveTheme(DropGamesSettings settings, string? requestedThemeId, string defaultThemeId)
        {
            var targetId = !string.IsNullOrWhiteSpace(requestedThemeId) ? requestedThemeId : defaultThemeId;
            return settings.Themes.FirstOrDefault(t => t.Id.Equals(targetId, StringComparison.OrdinalIgnoreCase))
                ?? settings.Themes.FirstOrDefault()
                ?? DropGameTheme.CreateDefaultSeasonalThemes()[0];
        }

        public async Task<string> TriggerParachuteDropAsync(
            CommandEventArgs eventArgs,
            string? emoteOrImage = null,
            string? themeOverrideId = null,
            double? forcedStartX = null,
            double? forcedWindAccelX = null,
            bool skipMinimumDurationCheck = false,
            bool allowDirectUrlOrPath = false)
        {
            CleanupExpiredDrops();
            var settings = await GetSettingsAsync();
            var pSettings = settings.Parachute;
            var theme = ResolveTheme(settings, themeOverrideId, pSettings.ActiveThemeId);
            var characterImageUrl = await ResolveCharacterOrEmoteUrlAsync(eventArgs, emoteOrImage, theme.DefaultCharacterImageUrl, allowDirectUrlOrPath);

            var durationSeconds = Math.Clamp(pSettings.DescentDurationSeconds, 2.5, 15.0);
            var totalTicks = Math.Max(60, (int)Math.Round(durationSeconds * TickRateHz));

            var startX = forcedStartX.HasValue
                ? Math.Clamp(forcedStartX.Value, 0.12, 0.88)
                : RandomDouble(0.18, 0.82);

            var startY = 0.06;
            var landingY = 0.88;
            var baseVy = (landingY - startY) / durationSeconds;

            var windScale = Math.Clamp(pSettings.WindStrength, 0.0, 1.0);
            var windAccelX = forcedWindAccelX ?? (RandomDouble(-0.028, 0.028) * windScale);
            var initialVx = forcedWindAccelX.HasValue ? 0.0 : RandomDouble(-0.035, 0.035) * Math.Max(0.25, windScale);
            var swayAmplitude = forcedStartX.HasValue && forcedWindAccelX.HasValue ? 0.0 : RandomDouble(0.012, 0.028);
            var swayFrequency = RandomDouble(1.6, 2.6);
            var swayPhase = RandomDouble(0, Math.PI * 2.0);

            // Determine Target platform positioning:
            // When a timed session is active, lock all drops to the session's randomized target location.
            // When forcedStartX is provided (e.g. test or explicit target), align target with forcedStartX.
            // Otherwise, use configured pSettings.TargetCenterX ensuring safe margins.
            var targetHalfWidth = Math.Clamp(pSettings.TargetWidthPercent / 200.0, 0.02, 0.35);
            var bullseyeHalfWidth = Math.Min(targetHalfWidth, Math.Clamp(pSettings.BullseyeWidthPercent / 200.0, 0.008, 0.15));

            var minSafeX = Math.Max(0.12, targetHalfWidth + 0.03);
            var maxSafeX = Math.Min(0.88, 1.0 - targetHalfWidth - 0.03);
            if (minSafeX >= maxSafeX)
            {
                minSafeX = 0.20;
                maxSafeX = 0.80;
            }

            double targetCenterX;
            if (_sessions.TryGetValue(DropGameType.Parachute, out var existingSession) && existingSession.EndsAtUtc > DateTime.UtcNow && existingSession.TargetCenterX.HasValue)
            {
                targetCenterX = Math.Clamp(existingSession.TargetCenterX.Value, minSafeX, maxSafeX);
            }
            else if (forcedStartX.HasValue && forcedWindAccelX.HasValue)
            {
                // Explicit forced trajectory (tests/guaranteed drops)
                targetCenterX = Math.Clamp(forcedStartX.Value, minSafeX, maxSafeX);
            }
            else
            {
                targetCenterX = Math.Clamp(pSettings.TargetCenterX, minSafeX, maxSafeX);
            }

            var dropToken = RandomNumberGenerator.GetHexString(64);
            var now = DateTime.UtcNow;

            var record = new PendingDropRecord
            {
                DropToken = dropToken,
                GameType = DropGameType.Parachute,
                Username = string.IsNullOrWhiteSpace(eventArgs.Name) ? "viewer" : eventArgs.Name,
                DisplayName = string.IsNullOrWhiteSpace(eventArgs.DisplayName) ? (string.IsNullOrWhiteSpace(eventArgs.Name) ? "Viewer" : eventArgs.Name) : eventArgs.DisplayName,
                UserId = eventArgs.UserId ?? "",
                EventArgs = eventArgs,
                Theme = theme,
                CreatedAtUtc = now,
                EarliestCompletionUtc = skipMinimumDurationCheck
                    ? now.AddMilliseconds(-100)
                    : now.AddSeconds(Math.Max(1.0, durationSeconds * 0.55)),
                ExpiresAtUtc = now.AddSeconds(Math.Max(60.0, durationSeconds + 30.0)),
                StartX = startX,
                StartY = startY,
                LandingY = landingY,
                InitialVx = initialVx,
                InitialVy = baseVy,
                WindAccelX = windAccelX,
                SwayAmplitude = swayAmplitude,
                SwayFrequency = swayFrequency,
                SwayPhase = swayPhase,
                TotalTicks = totalTicks,
                TargetCenterX = targetCenterX,
                TargetHalfWidth = targetHalfWidth,
                BullseyeHalfWidth = bullseyeHalfWidth,
                WinOnBullseyeOnly = pSettings.WinOnBullseyeOnly
            };

            lock (_simulationLock)
            {
                SimulateParachuteTrajectory(record, elapsedTicksAtDropletSpawn: 0, droplets: []);
                _pendingDrops[dropToken] = record;
            }

            // If session is active, increment drop counts
            DropGameSessionDto? activeSessionDto = null;
            var userKey = !string.IsNullOrWhiteSpace(eventArgs.UserId) ? eventArgs.UserId : record.Username;
            if (_sessions.TryGetValue(DropGameType.Parachute, out var pSession) && pSession.EndsAtUtc > now)
            {
                Interlocked.Increment(ref pSession.TotalDrops);
                pSession.UserDropCounts.AddOrUpdate(userKey, 1, (_, current) => current + 1);
                var rem = Math.Max(0, (int)Math.Ceiling((pSession.EndsAtUtc - now).TotalSeconds));
                activeSessionDto = new DropGameSessionDto
                {
                    GameType = "parachute",
                    IsActive = true,
                    TotalDurationSeconds = pSession.TotalDurationSeconds,
                    RemainingSeconds = rem,
                    StartedAtMs = new DateTimeOffset(pSession.StartedAtUtc).ToUnixTimeMilliseconds(),
                    EndsAtMs = new DateTimeOffset(pSession.EndsAtUtc).ToUnixTimeMilliseconds(),
                    TotalDrops = Volatile.Read(ref pSession.TotalDrops),
                    Theme = theme,
                    TargetCenterX = pSession.TargetCenterX,
                    TargetHalfWidth = pSession.TargetHalfWidth,
                    BullseyeHalfWidth = pSession.BullseyeHalfWidth
                };
            }

            var payload = new
            {
                type = "parachute_drop",
                dropToken,
                username = record.Username,
                displayName = record.DisplayName,
                characterImageUrl = characterImageUrl ?? "",
                isCustomEmote = !string.IsNullOrWhiteSpace(characterImageUrl) && !string.Equals(characterImageUrl, theme.DefaultCharacterImageUrl, StringComparison.OrdinalIgnoreCase),
                theme,
                session = activeSessionDto,
                physics = new
                {
                    startX = record.StartX,
                    startY = record.StartY,
                    landingY = record.LandingY,
                    initialVx = record.InitialVx,
                    initialVy = record.InitialVy,
                    windAccelX = record.WindAccelX,
                    swayAmplitude = record.SwayAmplitude,
                    swayFrequency = record.SwayFrequency,
                    swayPhase = record.SwayPhase,
                    totalTicks = record.TotalTicks,
                    durationSeconds,
                    landingX = record.LandingX,
                    targetCenterX = record.TargetCenterX,
                    targetHalfWidth = record.TargetHalfWidth,
                    bullseyeHalfWidth = record.BullseyeHalfWidth,
                    score = record.Score,
                    isHit = record.IsHit,
                    isBullseye = record.IsBullseye,
                    isWin = record.IsWin,
                    keyframes = record.Keyframes
                }
            };

            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            await QueueWsMessageAsync(WsTopics.Parachute, json, "parachute drop");
            return dropToken;
        }

        public async Task<List<DropletInstanceDto>> SpawnDropletsAsync(DropletSpawnRequest request)
        {
            CleanupExpiredDrops();
            var settings = await GetSettingsAsync();
            var pSettings = settings.Parachute;
            var theme = ResolveTheme(settings, null, pSettings.ActiveThemeId);

            var count = Math.Clamp(request.Count > 0 ? request.Count : pSettings.DefaultDropletCount, 1, 30);
            var sizePreset = !string.IsNullOrWhiteSpace(request.SizePreset) ? request.SizePreset : pSettings.DefaultDropletSize;
            var speedPreset = !string.IsNullOrWhiteSpace(request.SpeedPreset) ? request.SpeedPreset : pSettings.DefaultDropletSpeed;
            var pattern = !string.IsNullOrWhiteSpace(request.SpawnPattern) ? request.SpawnPattern : pSettings.DefaultDropletPattern;
            var knockbackMultiplier = request.KnockbackMultiplier > 0 ? Math.Clamp(request.KnockbackMultiplier, 0.1, 5.0) : pSettings.DefaultKnockbackMultiplier;

            var fakeEventArgs = new CommandEventArgs
            {
                Arg = request.EmoteOrImage ?? "",
                EmoteUrl = request.NativeEmoteUrl
            };
            var dropletImageUrl = await ResolveCharacterOrEmoteUrlAsync(fakeEventArgs, request.EmoteOrImage, theme.DefaultDropletImageUrl, request.AllowDirectUrlOrPath) ?? "";

            var droplets = new List<DropletInstanceDto>(count);
            var trajectoryUpdates = new List<ParachuteTrajectoryUpdateDto>();
            var now = DateTime.UtcNow;

            lock (_simulationLock)
            {
                var activeParachutes = _pendingDrops.Values
                    .Where(d => d.GameType == DropGameType.Parachute && d.ExpiresAtUtc > now)
                    .ToList();

                for (var i = 0; i < count; i++)
                {
                    var (sizeLabel, sizeScale, radius, mass) = ResolveDropletSize(sizePreset);
                    var (speedLabel, speedScale) = ResolveDropletSpeed(speedPreset);

                    var spawnTickOffset = i * 4; // Staggered burst every ~66ms
                    double startX, startY, vx, vy, gravity;

                    // If Targeted and there is an active parachute, aim toward its current/predicted mid-air position!
                    var targetDrop = activeParachutes.Count > 0
                        ? activeParachutes[i % activeParachutes.Count]
                        : null;

                    if (pattern.Equals("Targeted", StringComparison.OrdinalIgnoreCase) && targetDrop != null)
                    {
                        var elapsedSeconds = Math.Max(0.0, (now - targetDrop.CreatedAtUtc).TotalSeconds);
                        var currentTick = Math.Clamp((int)Math.Round(elapsedSeconds * TickRateHz) + spawnTickOffset, 0, targetDrop.TotalTicks - 10);
                        var targetPoint = GetParachutePositionAtTick(targetDrop, Math.Min(targetDrop.TotalTicks - 5, currentTick + 24));

                        var fromLeft = (i % 2 == 0) ? (targetPoint.X > 0.35) : (targetPoint.X < 0.65);
                        startX = fromLeft ? Math.Max(0.04, targetPoint.X - 0.28) : Math.Min(0.96, targetPoint.X + 0.28);
                        startY = Math.Max(0.04, targetPoint.Y - 0.22);

                        var interceptTime = 24.0 * Dt / Math.Max(0.6, speedScale);
                        vx = (targetPoint.X - startX) / interceptTime;
                        vy = (targetPoint.Y - startY) / interceptTime;
                        gravity = 0.08 * speedScale;
                    }
                    else if (pattern.Equals("SideWind", StringComparison.OrdinalIgnoreCase))
                    {
                        var fromLeft = i % 2 == 0;
                        startX = fromLeft ? 0.02 : 0.98;
                        startY = RandomDouble(0.12, 0.62);
                        vx = (fromLeft ? 1.0 : -1.0) * RandomDouble(0.32, 0.52) * speedScale;
                        vy = RandomDouble(0.12, 0.26) * speedScale;
                        gravity = 0.14 * speedScale;
                    }
                    else if (pattern.Equals("Fountain", StringComparison.OrdinalIgnoreCase))
                    {
                        var fromLeft = i % 2 == 0;
                        startX = fromLeft ? RandomDouble(0.08, 0.25) : RandomDouble(0.75, 0.92);
                        startY = 0.85;
                        vx = (fromLeft ? 1.0 : -1.0) * RandomDouble(0.22, 0.40) * speedScale;
                        vy = -RandomDouble(0.55, 0.78) * speedScale;
                        gravity = 0.52 * speedScale;
                    }
                    else // TopRain
                    {
                        startX = RandomDouble(0.12, 0.88);
                        startY = -0.04;
                        vx = RandomDouble(-0.08, 0.08) * speedScale;
                        vy = RandomDouble(0.35, 0.55) * speedScale;
                        gravity = 0.18 * speedScale;
                    }

                    droplets.Add(new DropletInstanceDto
                    {
                        Id = $"drp_{RandomNumberGenerator.GetHexString(8)}",
                        SpawnTickOffset = spawnTickOffset,
                        StartX = Math.Round(startX, 4),
                        StartY = Math.Round(startY, 4),
                        Vx = Math.Round(vx, 4),
                        Vy = Math.Round(vy, 4),
                        Gravity = Math.Round(gravity, 4),
                        Radius = Math.Round(radius, 4),
                        Mass = Math.Round(mass * knockbackMultiplier, 3),
                        SizeScale = Math.Round(sizeScale, 2),
                        SpeedScale = Math.Round(speedScale, 2),
                        SizeLabel = sizeLabel,
                        SpeedLabel = speedLabel,
                        ImageUrl = dropletImageUrl
                    });
                }

                // Step active parachutes through the 60Hz collision simulation with these droplets
                foreach (var drop in activeParachutes)
                {
                    var elapsedTicks = Math.Clamp((int)Math.Round((now - drop.CreatedAtUtc).TotalSeconds * TickRateHz), 0, drop.TotalTicks - 1);
                    SimulateParachuteTrajectory(drop, elapsedTicks, droplets);

                    trajectoryUpdates.Add(new ParachuteTrajectoryUpdateDto
                    {
                        DropToken = drop.DropToken,
                        LandingX = drop.LandingX,
                        TotalTicks = drop.TotalTicks,
                        Score = drop.Score,
                        IsHit = drop.IsHit,
                        IsBullseye = drop.IsBullseye,
                        IsWin = drop.IsWin,
                        DropletHits = drop.DropletHitCount,
                        Collisions = drop.Collisions,
                        Keyframes = drop.Keyframes
                    });
                }
            }

            var payload = new
            {
                type = "parachute_droplets",
                triggeredBy = request.TriggeredByUser ?? "Stream",
                theme,
                droplets,
                trajectoryUpdates
            };

            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            await QueueWsMessageAsync(WsTopics.Parachute, json, "parachute droplets");
            return droplets;
        }

        private static ParachuteTrajectoryPointDto GetParachutePositionAtTick(PendingDropRecord record, int tick)
        {
            if (record.Keyframes.Count > 0)
            {
                var nearest = record.Keyframes.OrderBy(k => Math.Abs(k.Tick - tick)).First();
                return nearest;
            }

            var t = tick * Dt;
            var x = Math.Clamp(record.StartX + record.InitialVx * t + 0.5 * record.WindAccelX * t * t, 0.06, 0.94);
            var y = Math.Clamp(record.StartY + record.InitialVy * t, record.StartY, record.LandingY);
            return new ParachuteTrajectoryPointDto { Tick = tick, X = x, Y = y, Vx = record.InitialVx, Vy = record.InitialVy };
        }

        /// <summary>
        /// Deterministic 60Hz fixed-timestep simulation for Parachute Drop + mid-air Droplet collisions.
        /// Preserves existing trajectory up to elapsedTicksAtDropletSpawn and simulates droplet impulses forward.
        /// </summary>
        private static void SimulateParachuteTrajectory(
            PendingDropRecord record,
            int elapsedTicksAtDropletSpawn,
            List<DropletInstanceDto> droplets)
        {
            double x = record.StartX;
            double y = record.StartY;
            double vx = record.InitialVx;
            double vy = record.InitialVy;

            var existingCollisions = elapsedTicksAtDropletSpawn > 0
                ? record.Collisions.Where(c => c.Tick < elapsedTicksAtDropletSpawn).ToList()
                : [];

            var startTick = 0;
            if (elapsedTicksAtDropletSpawn > 0 && record.Keyframes.Count > 0)
            {
                var priorPoint = record.Keyframes
                    .Where(k => k.Tick <= elapsedTicksAtDropletSpawn)
                    .OrderByDescending(k => k.Tick)
                    .FirstOrDefault();

                if (priorPoint != null)
                {
                    startTick = priorPoint.Tick;
                    x = priorPoint.X;
                    y = priorPoint.Y;
                    vx = priorPoint.Vx;
                    vy = priorPoint.Vy;
                }
                else
                {
                    startTick = Math.Max(0, elapsedTicksAtDropletSpawn);
                }
            }

            var keyframes = startTick > 0
                ? record.Keyframes.Where(k => k.Tick < startTick).ToList()
                : [];

            var hitDropletIds = new HashSet<string>(StringComparer.Ordinal);
            var maxAllowedTicks = Math.Max(record.TotalTicks, 60) + 180; // Bounded cap (~3 seconds extra if vertical speed was dampened)
            var finalTick = startTick;

            for (var tick = startTick; tick <= maxAllowedTicks; tick++)
            {
                finalTick = tick;
                var t = tick * Dt;

                // Check collisions with any active droplets at this tick
                if (droplets.Count > 0 && y < record.LandingY - 0.02)
                {
                    var localTick = tick - elapsedTicksAtDropletSpawn;
                    foreach (var droplet in droplets)
                    {
                        if (localTick < droplet.SpawnTickOffset || hitDropletIds.Contains(droplet.Id))
                        {
                            continue;
                        }

                        var dtDroplet = (localTick - droplet.SpawnTickOffset) * Dt;
                        var dx = droplet.StartX + droplet.Vx * dtDroplet;
                        var dy = droplet.StartY + droplet.Vy * dtDroplet + 0.5 * droplet.Gravity * dtDroplet * dtDroplet;
                        var dvx = droplet.Vx;
                        var dvy = droplet.Vy + droplet.Gravity * dtDroplet;

                        var distX = x - dx;
                        var distY = y - dy;
                        var distSq = distX * distX + distY * distY;
                        var minDist = ParachuteRadius + droplet.Radius;

                        if (distSq <= minDist * minDist)
                        {
                            hitDropletIds.Add(droplet.Id);
                            var dist = Math.Sqrt(Math.Max(0.0001, distSq));
                            var nx = distX / dist;
                            var ny = distY / dist;

                            // Relative velocity and momentum impulse scaled by droplet mass (size) and speed
                            var relVx = dvx - vx;
                            var relVy = dvy - vy;
                            var impulseMagnitude = 0.085 * droplet.Mass * Math.Max(0.6, droplet.SpeedScale);

                            // Push horizontally in the direction of droplet travel + collision normal
                            var pushDirectionX = Math.Abs(dvx) > 0.04 ? Math.Sign(dvx) : (Math.Abs(nx) > 0.05 ? Math.Sign(nx) : 1.0);
                            var deltaVx = (pushDirectionX * impulseMagnitude) + (relVx * 0.22 * Math.Min(2.5, droplet.Mass));
                            var deltaVy = Math.Clamp((ny * impulseMagnitude * 0.25) + (relVy * 0.08), -0.06, 0.08);

                            vx = Math.Clamp(vx + deltaVx, -0.45, 0.45);
                            vy = Math.Clamp(vy + deltaVy, record.InitialVy * 0.55, record.InitialVy * 1.85);

                            existingCollisions.Add(new ParachuteCollisionEventDto
                            {
                                Tick = tick,
                                DropletId = droplet.Id,
                                X = Math.Round(x, 4),
                                Y = Math.Round(y, 4),
                                DeltaVx = Math.Round(deltaVx, 4),
                                DeltaVy = Math.Round(deltaVy, 4),
                                SizeScale = droplet.SizeScale
                            });
                        }
                    }
                }

                if (tick % 6 == 0 || tick >= record.TotalTicks)
                {
                    keyframes.Add(new ParachuteTrajectoryPointDto
                    {
                        Tick = tick,
                        X = Math.Round(x, 4),
                        Y = Math.Round(y, 4),
                        Vx = Math.Round(vx, 4),
                        Vy = Math.Round(vy, 4)
                    });
                }

                if (y >= record.LandingY)
                {
                    break;
                }

                // Step physics (wind + air drag + sway)
                var swayVelX = record.SwayAmplitude * record.SwayFrequency * Math.Cos((record.SwayFrequency * t) + record.SwayPhase) * 0.35;
                vx = (vx + record.WindAccelX * Dt) * 0.992; // Gentle horizontal air drag
                vy = record.InitialVy + (vy - record.InitialVy) * 0.96; // Canopy stabilizes vertical speed back toward terminal velocity

                x += (vx + swayVelX) * Dt;
                y += vy * Dt;

                // Soft bounce off left/right screen margins
                if (x < 0.05)
                {
                    x = 0.05;
                    vx = Math.Abs(vx) * 0.65;
                }
                else if (x > 0.95)
                {
                    x = 0.95;
                    vx = -Math.Abs(vx) * 0.65;
                }
            }

            record.TotalTicks = finalTick;
            record.LandingX = Math.Round(Math.Clamp(x, 0.05, 0.95), 4);
            record.Collisions = existingCollisions;
            record.DropletHitCount = existingCollisions.Count;
            record.Keyframes = keyframes;

            // Evaluate landing accuracy against Target & Bullseye
            var distanceFromCenter = Math.Abs(record.LandingX - record.TargetCenterX);
            record.IsBullseye = distanceFromCenter <= record.BullseyeHalfWidth;
            record.IsHit = distanceFromCenter <= record.TargetHalfWidth;

            if (record.IsBullseye)
            {
                record.Score = 100;
            }
            else if (record.IsHit)
            {
                var normalizedDist = (distanceFromCenter - record.BullseyeHalfWidth) / Math.Max(0.001, record.TargetHalfWidth - record.BullseyeHalfWidth);
                record.Score = Math.Clamp((int)Math.Round(90.0 - (normalizedDist * 65.0)), 25, 95);
            }
            else
            {
                record.Score = 0;
            }

            record.IsWin = record.WinOnBullseyeOnly ? record.IsBullseye : record.IsHit;
        }

        private static (string Label, double Scale, double Radius, double Mass) ResolveDropletSize(string preset)
        {
            var normalized = preset?.Trim() ?? "Medium";
            if (normalized.Equals("Random", StringComparison.OrdinalIgnoreCase))
            {
                string[] choices = ["Small", "Medium", "Large", "Giant"];
                normalized = choices[RandomNumberGenerator.GetInt32(0, choices.Length)];
            }

            return normalized.ToLowerInvariant() switch
            {
                "small" => ("Small", 0.65, 0.018, 0.55),
                "large" => ("Large", 1.55, 0.042, 1.75),
                "giant" => ("Giant", 2.25, 0.060, 2.85),
                _ => ("Medium", 1.0, 0.028, 1.0)
            };
        }

        private static (string Label, double Scale) ResolveDropletSpeed(string preset)
        {
            var normalized = preset?.Trim() ?? "Normal";
            if (normalized.Equals("Random", StringComparison.OrdinalIgnoreCase))
            {
                string[] choices = ["Slow", "Normal", "Fast", "Hyper"];
                normalized = choices[RandomNumberGenerator.GetInt32(0, choices.Length)];
            }

            return normalized.ToLowerInvariant() switch
            {
                "slow" => ("Slow", 0.65),
                "fast" => ("Fast", 1.60),
                "hyper" => ("Hyper", 2.20),
                _ => ("Normal", 1.0)
            };
        }

        public async Task<string> TriggerPlinkoDropAsync(
            CommandEventArgs eventArgs,
            string? emoteOrImage = null,
            string? themeOverrideId = null,
            int? forcedBucketIndex = null,
            bool skipMinimumDurationCheck = false,
            bool allowDirectUrlOrPath = false)
        {
            CleanupExpiredDrops();
            var settings = await GetSettingsAsync();
            var plinkoSettings = settings.Plinko;
            var theme = ResolveTheme(settings, themeOverrideId, plinkoSettings.ActiveThemeId);
            var characterImageUrl = await ResolveCharacterOrEmoteUrlAsync(eventArgs, emoteOrImage, theme.DefaultCharacterImageUrl, allowDirectUrlOrPath);

            var buckets = plinkoSettings.Buckets.Count > 0
                ? plinkoSettings.Buckets
                : PlinkoGameSettings.CreateDefaultBuckets();

            var rows = Math.Clamp(plinkoSettings.RowCount, 5, 14);
            var durationSeconds = Math.Clamp(plinkoSettings.DropDurationSeconds, 2.0, 12.0);

            int winningBucketIndex;
            if (forcedBucketIndex.HasValue)
            {
                winningBucketIndex = Math.Clamp(forcedBucketIndex.Value, 0, buckets.Count - 1);
            }
            else
            {
                winningBucketIndex = PickWeightedBucketIndex(buckets);
            }

            var winningBucket = buckets[winningBucketIndex];
            var effectiveScore = winningBucket.IsWin ? winningBucket.Score : 0;
            var waypoints = BuildPlinkoWaypoints(rows, buckets.Count, winningBucketIndex);

            var dropToken = RandomNumberGenerator.GetHexString(64);
            var now = DateTime.UtcNow;

            var record = new PendingDropRecord
            {
                DropToken = dropToken,
                GameType = DropGameType.Plinko,
                Username = string.IsNullOrWhiteSpace(eventArgs.Name) ? "viewer" : eventArgs.Name,
                DisplayName = string.IsNullOrWhiteSpace(eventArgs.DisplayName) ? (string.IsNullOrWhiteSpace(eventArgs.Name) ? "Viewer" : eventArgs.Name) : eventArgs.DisplayName,
                UserId = eventArgs.UserId ?? "",
                EventArgs = eventArgs,
                Theme = theme,
                CreatedAtUtc = now,
                EarliestCompletionUtc = skipMinimumDurationCheck
                    ? now.AddMilliseconds(-100)
                    : now.AddSeconds(Math.Max(1.0, durationSeconds * 0.55)),
                ExpiresAtUtc = now.AddSeconds(Math.Max(60.0, durationSeconds + 30.0)),
                StartX = waypoints.First().X,
                LandingX = waypoints.Last().X,
                Score = effectiveScore,
                IsHit = winningBucket.IsWin,
                IsBullseye = winningBucket.IsWin && winningBucket.Score >= buckets.Where(b => b.IsWin).Select(b => b.Score).DefaultIfEmpty(0).Max(),
                IsWin = winningBucket.IsWin,
                BucketIndex = winningBucketIndex,
                BucketLabel = winningBucket.Label
            };

            _pendingDrops[dropToken] = record;

            // If session is active, increment drop counts
            DropGameSessionDto? activeSessionDto = null;
            var userKey = !string.IsNullOrWhiteSpace(eventArgs.UserId) ? eventArgs.UserId : record.Username;
            if (_sessions.TryGetValue(DropGameType.Plinko, out var plinkoSession) && plinkoSession.EndsAtUtc > now)
            {
                Interlocked.Increment(ref plinkoSession.TotalDrops);
                plinkoSession.UserDropCounts.AddOrUpdate(userKey, 1, (_, current) => current + 1);
                var rem = Math.Max(0, (int)Math.Ceiling((plinkoSession.EndsAtUtc - now).TotalSeconds));
                activeSessionDto = new DropGameSessionDto
                {
                    GameType = "plinko",
                    IsActive = true,
                    TotalDurationSeconds = plinkoSession.TotalDurationSeconds,
                    RemainingSeconds = rem,
                    StartedAtMs = new DateTimeOffset(plinkoSession.StartedAtUtc).ToUnixTimeMilliseconds(),
                    EndsAtMs = new DateTimeOffset(plinkoSession.EndsAtUtc).ToUnixTimeMilliseconds(),
                    TotalDrops = Volatile.Read(ref plinkoSession.TotalDrops),
                    Theme = theme
                };
            }

            var payload = new
            {
                type = "plinko_drop",
                dropToken,
                username = record.Username,
                displayName = record.DisplayName,
                characterImageUrl = characterImageUrl ?? "",
                isCustomEmote = !string.IsNullOrWhiteSpace(characterImageUrl) && !string.Equals(characterImageUrl, theme.DefaultCharacterImageUrl, StringComparison.OrdinalIgnoreCase),
                theme,
                session = activeSessionDto,
                board = new
                {
                    rowCount = rows,
                    durationSeconds,
                    buckets,
                    winningBucketIndex,
                    winningBucketLabel = winningBucket.Label,
                    score = effectiveScore,
                    isWin = winningBucket.IsWin,
                    waypoints
                }
            };

            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            await QueueWsMessageAsync(WsTopics.Plinko, json, "plinko drop");
            return dropToken;
        }

        private static int PickWeightedBucketIndex(List<PlinkoBucket> buckets)
        {
            var totalWeight = buckets.Sum(b => Math.Max(1, b.Weight));
            var roll = RandomNumberGenerator.GetInt32(0, totalWeight);
            var cumulative = 0;
            for (var i = 0; i < buckets.Count; i++)
            {
                cumulative += Math.Max(1, buckets[i].Weight);
                if (roll < cumulative)
                {
                    return i;
                }
            }
            return buckets.Count / 2;
        }

        private static List<PlinkoWaypointDto> BuildPlinkoWaypoints(int rows, int bucketCount, int targetBucketIndex)
        {
            var waypoints = new List<PlinkoWaypointDto>(rows + 2);
            var targetX = (targetBucketIndex + 0.5) / bucketCount;
            var currentX = Math.Clamp(0.5 + RandomDouble(-0.08, 0.08), 0.25, 0.75);

            waypoints.Add(new PlinkoWaypointDto
            {
                Row = -1,
                Col = 0,
                X = Math.Round(currentX, 4),
                Y = 0.08
            });

            for (var r = 0; r < rows; r++)
            {
                var progress = (double)(r + 1) / rows;
                var rowY = 0.16 + (progress * 0.64);
                var remainingRows = rows - r;

                // Guide token smoothly toward targetX while bouncing left/right off pegs
                var idealStep = (targetX - currentX) / Math.Max(1, remainingRows);
                var jitter = (RandomNumberGenerator.GetInt32(0, 2) == 0 ? -1 : 1) * (0.032 * (1.0 - (progress * 0.5)));
                currentX = Math.Clamp(currentX + idealStep + jitter, 0.08, 0.92);

                waypoints.Add(new PlinkoWaypointDto
                {
                    Row = r,
                    Col = (int)Math.Round(currentX * bucketCount),
                    X = Math.Round(currentX, 4),
                    Y = Math.Round(rowY, 4)
                });
            }

            waypoints.Add(new PlinkoWaypointDto
            {
                Row = rows,
                Col = targetBucketIndex,
                X = Math.Round(targetX, 4),
                Y = 0.88
            });

            return waypoints;
        }

        public async Task<bool> CompleteDropAsync(string dropToken)
        {
            if (string.IsNullOrWhiteSpace(dropToken))
            {
                return false;
            }

            if (!_pendingDrops.TryGetValue(dropToken, out var peekRecord))
            {
                logger.LogWarning("Rejected unknown or already-consumed dropToken completion request.");
                return false;
            }

            var now = DateTime.UtcNow;
            if (now > peekRecord.ExpiresAtUtc)
            {
                _pendingDrops.TryRemove(dropToken, out _);
                logger.LogWarning("Rejected expired dropToken for {GameType} user {User}.", peekRecord.GameType, peekRecord.Username);
                return false;
            }

            if (now < peekRecord.EarliestCompletionUtc)
            {
                logger.LogWarning("Rejected premature dropToken completion for {GameType} user {User} (arrived before minimum flight time).", peekRecord.GameType, peekRecord.Username);
                return false;
            }

            // Atomic single-use consumption so replay attacks cannot re-trigger actions
            if (!_pendingDrops.TryRemove(dropToken, out var record))
            {
                return false;
            }

            var settings = await GetSettingsAsync();
            if (record.GameType == DropGameType.Parachute)
            {
                await ProcessParachuteCompletionAsync(record, settings.Parachute);
            }
            else
            {
                await ProcessPlinkoCompletionAsync(record, settings.Plinko);
            }

            return true;
        }

        private async Task ProcessParachuteCompletionAsync(PendingDropRecord record, ParachuteGameSettings pSettings)
        {
            var distanceFromCenter = Math.Abs(record.LandingX - record.TargetCenterX);
            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["User"] = record.DisplayName,
                ["Target"] = record.DisplayName,
                ["Score"] = record.Score.ToString(CultureInfo.InvariantCulture),
                ["IsWin"] = record.IsWin ? "true" : "false",
                ["IsBullseye"] = record.IsBullseye ? "true" : "false",
                ["DropletHits"] = record.DropletHitCount.ToString(CultureInfo.InvariantCulture),
                ["DistanceFromCenter"] = Math.Round(distanceFromCenter * 100.0, 1).ToString(CultureInfo.InvariantCulture),
                ["Theme"] = record.Theme.Name,
                ["Game"] = "ParachuteDrop"
            };

            var eventType = record.IsBullseye
                ? DefaultCommandEventTypes.ParachuteBullseye
                : (record.IsWin ? DefaultCommandEventTypes.ParachuteWin : DefaultCommandEventTypes.ParachuteLose);

            await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                "drop",
                eventType,
                record.EventArgs,
                variables);

            // If it was a bullseye, also fire the general "win" trigger so actions listening to "win" run as well
            if (record.IsBullseye)
            {
                await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                    "drop",
                    DefaultCommandEventTypes.ParachuteWin,
                    record.EventArgs,
                    variables);
            }

            if (record.IsWin)
            {
                if (!string.IsNullOrWhiteSpace(pSettings.WinActionName))
                {
                    await EnqueueConfiguredWinActionAsync(pSettings.WinActionName, record.EventArgs, variables);
                }

                if (pSettings.AnnounceWinsInChat && !string.IsNullOrWhiteSpace(pSettings.WinChatMessageTemplate))
                {
                    var bullseyeTag = record.IsBullseye ? " 🎯 BULLSEYE!" : "";
                    var msg = pSettings.WinChatMessageTemplate
                        .Replace("{user}", record.DisplayName, StringComparison.OrdinalIgnoreCase)
                        .Replace("{score}", record.Score.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                        .Replace("{bullseye}", bullseyeTag, StringComparison.OrdinalIgnoreCase)
                        .Replace("{droplethits}", record.DropletHitCount.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
                    await ServiceBackbone.SendChatMessage(msg);
                }
            }
        }

        private async Task ProcessPlinkoCompletionAsync(PendingDropRecord record, PlinkoGameSettings plinkoSettings)
        {
            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["User"] = record.DisplayName,
                ["Target"] = record.DisplayName,
                ["Score"] = record.Score.ToString(CultureInfo.InvariantCulture),
                ["BucketLabel"] = record.BucketLabel,
                ["BucketIndex"] = record.BucketIndex.ToString(CultureInfo.InvariantCulture),
                ["IsWin"] = record.IsWin ? "true" : "false",
                ["Theme"] = record.Theme.Name,
                ["Game"] = "PlinkoBounce"
            };

            var eventType = record.IsWin
                ? DefaultCommandEventTypes.PlinkoWin
                : DefaultCommandEventTypes.PlinkoLose;

            await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                "plinko",
                eventType,
                record.EventArgs,
                variables);

            if (record.IsWin)
            {
                if (!string.IsNullOrWhiteSpace(plinkoSettings.WinActionName))
                {
                    await EnqueueConfiguredWinActionAsync(plinkoSettings.WinActionName, record.EventArgs, variables);
                }

                if (plinkoSettings.AnnounceWinsInChat && !string.IsNullOrWhiteSpace(plinkoSettings.WinChatMessageTemplate))
                {
                    var msg = plinkoSettings.WinChatMessageTemplate
                        .Replace("{user}", record.DisplayName, StringComparison.OrdinalIgnoreCase)
                        .Replace("{score}", record.Score.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                        .Replace("{bucket}", record.BucketLabel, StringComparison.OrdinalIgnoreCase);
                    await ServiceBackbone.SendChatMessage(msg);
                }
            }
        }

        private async Task EnqueueConfiguredWinActionAsync(
            string actionName,
            CommandEventArgs eventArgs,
            Dictionary<string, string> gameVariables)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var actionManagement = scope.ServiceProvider.GetRequiredService<IActionManagementService>();
                var actionRunner = scope.ServiceProvider.GetRequiredService<IAction>();

                var allActions = await actionManagement.GetAllActionsAsync();
                var targetAction = allActions.FirstOrDefault(a =>
                    a.Enabled && a.Name.Equals(actionName.Trim(), StringComparison.OrdinalIgnoreCase));

                if (targetAction == null)
                {
                    logger.LogWarning("Configured DropGame win action '{ActionName}' was not found or is disabled.", actionName);
                    return;
                }

                var actionVars = CommandEventArgsConverter.ToDictionary(eventArgs);
                foreach (var kvp in gameVariables)
                {
                    actionVars[kvp.Key] = kvp.Value;
                }

                await actionRunner.EnqueueAction(actionVars, targetAction);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to enqueue win action '{ActionName}' for user {User}", actionName, eventArgs.DisplayName);
            }
        }

        private async Task QueueWsMessageAsync(string topic, string json, string description)
        {
            try
            {
                await webSocketMessenger.AddToQueue(topic, json);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to queue {Description} message to topic {Topic}", description, topic);
            }
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;

            var cmdName = command.CommandProperties.CommandName;
            var settings = await GetSettingsAsync();

            switch (cmdName)
            {
                case "drop":
                    {
                        var pSettings = settings.Parachute;
                        var firstArg = e.Args.Count > 0 ? e.Args[0] : (e.Arg ?? "").Trim();

                        // Moderator/Broadcaster session control commands
                        if (e.IsModOrHigher() && firstArg.Equals("start", StringComparison.OrdinalIgnoreCase))
                        {
                            int? customDuration = null;
                            if (e.Args.Count > 1 && int.TryParse(e.Args[1], out var parsedDuration))
                            {
                                customDuration = parsedDuration;
                            }
                            await StartSessionAsync(DropGameType.Parachute, customDuration, triggeredBy: e.DisplayName);
                            return;
                        }

                        if (e.IsModOrHigher() && firstArg.Equals("stop", StringComparison.OrdinalIgnoreCase))
                        {
                            await StopSessionAsync(DropGameType.Parachute, triggeredBy: e.DisplayName);
                            return;
                        }

                        // Session mode check
                        if (pSettings.SessionModeEnabled)
                        {
                            var session = GetCurrentSession(DropGameType.Parachute);
                            if (session == null)
                            {
                                if (pSettings.AutoStartOnCommand)
                                {
                                    await StartSessionAsync(DropGameType.Parachute, pSettings.DefaultSessionDurationSeconds, triggeredBy: e.DisplayName);
                                }
                                else
                                {
                                    // Session not active and auto-start disabled
                                    return;
                                }
                            }

                            // Check max drops per user limit
                            if (_sessions.TryGetValue(DropGameType.Parachute, out var activeState))
                            {
                                var userKey = !string.IsNullOrWhiteSpace(e.UserId) ? e.UserId : (e.Name ?? "viewer");
                                if (pSettings.MaxDropsPerUserPerSession > 0 &&
                                    activeState.UserDropCounts.TryGetValue(userKey, out var count) &&
                                    count >= pSettings.MaxDropsPerUserPerSession)
                                {
                                    // Max drops reached for this user in current session
                                    return;
                                }
                            }
                        }

                        await TriggerParachuteDropAsync(e, e.Arg);
                    }
                    break;

                case "droplets":
                    {
                        var pSettings = settings.Parachute;
                        await SpawnDropletsAsync(new DropletSpawnRequest
                        {
                            Count = pSettings.DefaultDropletCount,
                            SizePreset = pSettings.DefaultDropletSize,
                            SpeedPreset = pSettings.DefaultDropletSpeed,
                            SpawnPattern = pSettings.DefaultDropletPattern,
                            KnockbackMultiplier = pSettings.DefaultKnockbackMultiplier,
                            EmoteOrImage = e.Arg,
                            NativeEmoteUrl = e.EmoteUrl,
                            TriggeredByUser = e.DisplayName
                        });
                    }
                    break;

                case "plinko":
                    {
                        var plinkoSettings = settings.Plinko;
                        var firstArg = e.Args.Count > 0 ? e.Args[0] : (e.Arg ?? "").Trim();

                        // Moderator/Broadcaster session control commands
                        if (e.IsModOrHigher() && firstArg.Equals("start", StringComparison.OrdinalIgnoreCase))
                        {
                            int? customDuration = null;
                            if (e.Args.Count > 1 && int.TryParse(e.Args[1], out var parsedDuration))
                            {
                                customDuration = parsedDuration;
                            }
                            await StartSessionAsync(DropGameType.Plinko, customDuration, triggeredBy: e.DisplayName);
                            return;
                        }

                        if (e.IsModOrHigher() && firstArg.Equals("stop", StringComparison.OrdinalIgnoreCase))
                        {
                            await StopSessionAsync(DropGameType.Plinko, triggeredBy: e.DisplayName);
                            return;
                        }

                        // Session mode check
                        if (plinkoSettings.SessionModeEnabled)
                        {
                            var session = GetCurrentSession(DropGameType.Plinko);
                            if (session == null)
                            {
                                if (plinkoSettings.AutoStartOnCommand)
                                {
                                    await StartSessionAsync(DropGameType.Plinko, plinkoSettings.DefaultSessionDurationSeconds, triggeredBy: e.DisplayName);
                                }
                                else
                                {
                                    // Session not active and auto-start disabled
                                    return;
                                }
                            }

                            // Check max drops per user limit
                            if (_sessions.TryGetValue(DropGameType.Plinko, out var activeState))
                            {
                                var userKey = !string.IsNullOrWhiteSpace(e.UserId) ? e.UserId : (e.Name ?? "viewer");
                                if (plinkoSettings.MaxDropsPerUserPerSession > 0 &&
                                    activeState.UserDropCounts.TryGetValue(userKey, out var count) &&
                                    count >= plinkoSettings.MaxDropsPerUserPerSession)
                                {
                                    // Max drops reached for this user in current session
                                    return;
                                }
                            }
                        }

                        await TriggerPlinkoDropAsync(e, e.Arg);
                    }
                    break;
            }
        }

        public override async Task Register()
        {
            await RegisterDefaultCommand("drop", this, ModuleName, Rank.Viewer, userCooldown: 10);
            await RegisterDefaultCommand("droplets", this, ModuleName, Rank.Viewer, userCooldown: 15);
            await RegisterDefaultCommand("plinko", this, ModuleName, Rank.Viewer, userCooldown: 10);
            logger.LogInformation("Registered commands for {ModuleName}", ModuleName);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting {ModuleName}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopping {ModuleName}", ModuleName);
            _pendingDrops.Clear();
            foreach (var kvp in _sessions)
            {
                try
                {
                    kvp.Value.ExpiryTimer?.Dispose();
                    kvp.Value.Cts.Cancel();
                    kvp.Value.Cts.Dispose();
                }
                catch
                {
                    // Ignore on shutdown
                }
            }
            _sessions.Clear();
            return Task.CompletedTask;
        }

        private static double RandomDouble(double min, double max)
        {
            var u = RandomNumberGenerator.GetInt32(0, 1_000_000) / 1_000_000.0;
            return min + ((max - min) * u);
        }
    }
}
