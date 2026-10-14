using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Services;

namespace PenguinTwitchBot.Bot.Commands.Games.Parachute
{
    public class ParachuteDropService(
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        IDefaultCommandTriggerService defaultCommandTriggerService,
        IServiceScopeFactory scopeFactory,
        IEmoteService emoteService,
        IWebSocketMessenger webSocketMessenger,
        Application.Notifications.IPenguinDispatcher dispatcher,
        IDropGamesSettingsService settingsService,
        IDropRegistry dropRegistry,
        ILogger<ParachuteDropService> logger)
        : BaseCommandService(serviceBackbone, commandHandler, "ParachuteDrop", dispatcher), IHostedService, IParachuteDropService
    {
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        private readonly ConcurrentQueue<QueuedSessionRequest> _sessionQueue = new();
        private readonly SemaphoreSlim _sessionLock = new(1, 1);
        private readonly object _simulationLock = new();
        private DropGameSessionState? _activeSession;

        private sealed class QueuedSessionRequest
        {
            public int? DurationSeconds { get; init; }
            public string? ThemeOverrideId { get; init; }
            public string? TriggeredBy { get; init; }
            public DateTime EnqueuedAtUtc { get; init; } = DateTime.UtcNow;
        }

        private sealed class DropGameSessionState
        {
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

        public DropGameSessionDto? GetCurrentSession()
        {
            var state = _activeSession;
            if (state == null) return null;
            var now = DateTime.UtcNow;
            if (state.EndsAtUtc <= now) return null;

            var remainingSec = Math.Max(0, (int)Math.Ceiling((state.EndsAtUtc - now).TotalSeconds));
            return new DropGameSessionDto
            {
                GameType = "parachute",
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

        public int GetQueuedSessionCount() => _sessionQueue.Count;

        public async Task ClearSessionQueueAsync()
        {
            await _sessionLock.WaitAsync();
            try
            {
                while (_sessionQueue.TryDequeue(out _)) { }
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        private bool HasActiveTimedSession()
        {
            var state = _activeSession;
            return state != null && state.EndsAtUtc > DateTime.UtcNow;
        }

        private int RemoveQueuedSessions()
        {
            _sessionLock.Wait();
            try
            {
                var countRemoved = 0;
                while (_sessionQueue.TryDequeue(out _))
                {
                    countRemoved++;
                }
                return countRemoved;
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        private async Task ProcessNextQueuedSessionAsync()
        {
            QueuedSessionRequest? next = null;
            await _sessionLock.WaitAsync();
            try
            {
                if (HasActiveTimedSession()) return;
                if (_sessionQueue.TryDequeue(out var req))
                {
                    next = req;
                }
            }
            finally
            {
                _sessionLock.Release();
            }

            if (next != null)
            {
                try
                {
                    logger.LogInformation("Starting queued Parachute session ({Duration}s, triggered by {User})",
                        next.DurationSeconds, next.TriggeredBy ?? "Queue");
                    await StartSessionAsync(next.DurationSeconds, next.ThemeOverrideId, next.TriggeredBy);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to start queued Parachute session");
                }
            }
        }

        public async Task<DropGameSessionDto> StartSessionAsync(
            int? durationSeconds = null,
            string? themeOverrideId = null,
            string? triggeredBy = null)
        {
            var settings = await settingsService.GetSettingsAsync();
            var duration = durationSeconds.HasValue && durationSeconds.Value > 0
                ? durationSeconds.Value
                : settings.Parachute.DefaultSessionDurationSeconds;

            duration = Math.Clamp(duration, 5, 3600);

            await _sessionLock.WaitAsync();
            try
            {
                if (HasActiveTimedSession())
                {
                    var queuedItem = new QueuedSessionRequest
                    {
                        DurationSeconds = duration,
                        ThemeOverrideId = themeOverrideId,
                        TriggeredBy = triggeredBy,
                        EnqueuedAtUtc = DateTime.UtcNow
                    };
                    _sessionQueue.Enqueue(queuedItem);
                    var queuePosition = _sessionQueue.Count;

                    logger.LogInformation("Enqueued Parachute session for {Duration}s (Triggered by {User}) behind active round. Queue position: {Position}",
                        duration, triggeredBy ?? "System", queuePosition);

                    if (settings.Parachute.AnnounceSessionStatusInChat)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await ServiceBackbone.SendChatMessage($"⏳ Parachute Drop session has been queued behind active round (Queue position: #{queuePosition})!");
                            }
                            catch (Exception ex)
                            {
                                logger.LogDebug(ex, "Failed to send chat announcement for queued session");
                            }
                        });
                    }

                    return new DropGameSessionDto
                    {
                        GameType = "parachute",
                        IsActive = false,
                        IsQueued = true,
                        QueuePosition = queuePosition,
                        TotalDurationSeconds = duration,
                        RemainingSeconds = duration,
                        TotalDrops = 0
                    };
                }

                // If an existing session exists, dispose cleanly
                if (_activeSession != null)
                {
                    _activeSession.ExpiryTimer?.Dispose();
                    _activeSession.Cts.Cancel();
                    _activeSession.Cts.Dispose();
                    _activeSession = null;
                }

                var now = DateTime.UtcNow;
                var endsAt = now.AddSeconds(duration);

                var state = new DropGameSessionState
                {
                    StartedAtUtc = now,
                    EndsAtUtc = endsAt,
                    TotalDurationSeconds = duration,
                    ThemeIdOverride = themeOverrideId,
                    StartedBy = triggeredBy
                };

                state.ExpiryTimer = new Timer(async _ =>
                {
                    try
                    {
                        await StopSessionAsync(state, triggeredBy: "timer_expired");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error expiring session for Parachute");
                    }
                }, null, TimeSpan.FromSeconds(duration), Timeout.InfiniteTimeSpan);

                var theme = DropGameMediaResolver.ResolveTheme(settings, themeOverrideId, settings.Parachute.ActiveThemeId);

                var targetHalfWidth = Math.Clamp(settings.Parachute.TargetWidthPercent / 200.0, 0.02, 0.35);
                var bullseyeHalfWidth = targetHalfWidth * 0.25;

                var minCenterX = Math.Max(0.12, targetHalfWidth + 0.03);
                var maxCenterX = Math.Min(0.88, 1.0 - targetHalfWidth - 0.03);
                if (minCenterX >= maxCenterX)
                {
                    minCenterX = 0.20;
                    maxCenterX = 0.80;
                }

                state.TargetCenterX = Math.Round(DropGameMath.RandomDouble(minCenterX, maxCenterX), 4);
                state.TargetHalfWidth = targetHalfWidth;
                state.BullseyeHalfWidth = bullseyeHalfWidth;

                _activeSession = state;

                var dto = new DropGameSessionDto
                {
                    GameType = "parachute",
                    IsActive = true,
                    IsQueued = false,
                    QueuePosition = 0,
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

                var wsPayload = JsonSerializer.Serialize(new { type = "session_start", session = dto }, _jsonOptions);
                await QueueWsMessageAsync(WsTopics.Parachute, wsPayload, "Parachute session_start");

                if (settings.Parachute.AnnounceSessionStatusInChat)
                {
                    var template = settings.Parachute.SessionStartChatMessageTemplate;
                    if (!string.IsNullOrWhiteSpace(template))
                    {
                        var msg = template.Replace("{duration}", duration.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
                        await ServiceBackbone.SendChatMessage(msg);
                    }
                }

                logger.LogInformation("Started Parachute session for {Duration}s (Triggered by {User})", duration, triggeredBy ?? "System");
                return dto;
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        public async Task<DropGameSessionDto?> StopSessionAsync(string? triggeredBy = null)
        {
            var state = Interlocked.Exchange(ref _activeSession, null);
            if (state == null)
            {
                var removedCount = RemoveQueuedSessions();
                if (removedCount > 0)
                {
                    logger.LogInformation("Removed {Count} queued Parachute session(s) (Triggered by {User})", removedCount, triggeredBy ?? "System");
                    return new DropGameSessionDto { GameType = "parachute", IsActive = false, RemainingSeconds = 0 };
                }
                return null;
            }

            return await FinalizeStoppedSessionAsync(state, triggeredBy);
        }

        private async Task<DropGameSessionDto?> StopSessionAsync(DropGameSessionState expectedState, string? triggeredBy = null)
        {
            ArgumentNullException.ThrowIfNull(expectedState);
            if (Interlocked.CompareExchange(ref _activeSession, null, expectedState) != expectedState)
            {
                return null;
            }

            return await FinalizeStoppedSessionAsync(expectedState, triggeredBy);
        }

        private async Task<DropGameSessionDto> FinalizeStoppedSessionAsync(DropGameSessionState state, string? triggeredBy = null)
        {
            try
            {
                state.ExpiryTimer?.Dispose();
                state.Cts.Cancel();
                state.Cts.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error disposing session timer/cts for Parachute");
            }

            var totalDrops = Volatile.Read(ref state.TotalDrops);
            var settings = await settingsService.GetSettingsAsync();
            var theme = DropGameMediaResolver.ResolveTheme(settings, state.ThemeIdOverride, settings.Parachute.ActiveThemeId);

            var dto = new DropGameSessionDto
            {
                GameType = "parachute",
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

            var wsPayload = JsonSerializer.Serialize(new { type = "session_end", session = dto }, _jsonOptions);
            await QueueWsMessageAsync(WsTopics.Parachute, wsPayload, "Parachute session_end");

            if (!string.Equals(triggeredBy, "restart", StringComparison.OrdinalIgnoreCase))
            {
                if (settings.Parachute.AnnounceSessionStatusInChat)
                {
                    var template = settings.Parachute.SessionEndChatMessageTemplate;
                    if (!string.IsNullOrWhiteSpace(template))
                    {
                        var msg = template
                            .Replace("{count}", totalDrops.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                            .Replace("{duration}", state.TotalDurationSeconds.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
                        await ServiceBackbone.SendChatMessage(msg);
                    }
                }
            }

            logger.LogInformation("Stopped Parachute session with {Drops} total drops (Triggered by {User})", totalDrops, triggeredBy ?? "System");

            if (!string.Equals(triggeredBy, "restart", StringComparison.OrdinalIgnoreCase))
            {
                await ProcessNextQueuedSessionAsync();
            }

            return dto;
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
            dropRegistry.CleanupExpiredDrops();
            var settings = await settingsService.GetSettingsAsync();
            var pSettings = settings.Parachute;
            var theme = DropGameMediaResolver.ResolveTheme(settings, themeOverrideId, pSettings.ActiveThemeId);
            var characterImageUrl = await DropGameMediaResolver.ResolveCharacterOrEmoteUrlAsync(emoteService, eventArgs, emoteOrImage, theme.DefaultCharacterImageUrl, allowDirectUrlOrPath);

            var durationSeconds = Math.Clamp(pSettings.DescentDurationSeconds, 2.5, 15.0);
            var totalTicks = Math.Max(60, (int)Math.Round(durationSeconds * ParachuteSimulationEngine.TickRateHz));

            var startX = forcedStartX.HasValue
                ? Math.Clamp(forcedStartX.Value, 0.12, 0.88)
                : DropGameMath.RandomDouble(0.18, 0.82);

            var startY = 0.06;
            var landingY = 0.88;
            var baseVy = (landingY - startY) / durationSeconds;

            var windScale = Math.Clamp(pSettings.WindStrength, 0.0, 1.0);
            var windAccelX = forcedWindAccelX ?? (DropGameMath.RandomDouble(-0.028, 0.028) * windScale);
            var initialVx = forcedWindAccelX.HasValue ? 0.0 : DropGameMath.RandomDouble(-0.035, 0.035) * Math.Max(0.25, windScale);
            var swayAmplitude = forcedStartX.HasValue && forcedWindAccelX.HasValue ? 0.0 : DropGameMath.RandomDouble(0.012, 0.028);
            var swayFrequency = DropGameMath.RandomDouble(1.6, 2.6);
            var swayPhase = DropGameMath.RandomDouble(0, Math.PI * 2.0);

            var targetHalfWidth = Math.Clamp(pSettings.TargetWidthPercent / 200.0, 0.02, 0.35);
            var bullseyeHalfWidth = targetHalfWidth * 0.25;

            var minSafeX = Math.Max(0.12, targetHalfWidth + 0.03);
            var maxSafeX = Math.Min(0.88, 1.0 - targetHalfWidth - 0.03);
            if (minSafeX >= maxSafeX)
            {
                minSafeX = 0.20;
                maxSafeX = 0.80;
            }

            double targetCenterX;
            var currentSessionState = _activeSession;
            if (currentSessionState != null && currentSessionState.EndsAtUtc > DateTime.UtcNow && currentSessionState.TargetCenterX.HasValue)
            {
                targetCenterX = Math.Clamp(currentSessionState.TargetCenterX.Value, minSafeX, maxSafeX);
            }
            else if (forcedStartX.HasValue && forcedWindAccelX.HasValue)
            {
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
                TargetHalfWidth = targetHalfWidth
            };

            lock (_simulationLock)
            {
                ParachuteSimulationEngine.SimulateParachuteTrajectory(record, 0, []);
            }

            dropRegistry.RegisterDrop(record);

            DropGameSessionDto? activeSessionDto = null;
            var userKey = !string.IsNullOrWhiteSpace(eventArgs.UserId) ? eventArgs.UserId : record.Username;
            if (currentSessionState != null && currentSessionState.EndsAtUtc > now)
            {
                Interlocked.Increment(ref currentSessionState.TotalDrops);
                currentSessionState.UserDropCounts.AddOrUpdate(userKey, 1, (_, current) => current + 1);
                var rem = Math.Max(0, (int)Math.Ceiling((currentSessionState.EndsAtUtc - now).TotalSeconds));
                activeSessionDto = new DropGameSessionDto
                {
                    GameType = "parachute",
                    IsActive = true,
                    TotalDurationSeconds = currentSessionState.TotalDurationSeconds,
                    RemainingSeconds = rem,
                    StartedAtMs = new DateTimeOffset(currentSessionState.StartedAtUtc).ToUnixTimeMilliseconds(),
                    EndsAtMs = new DateTimeOffset(currentSessionState.EndsAtUtc).ToUnixTimeMilliseconds(),
                    TotalDrops = Volatile.Read(ref currentSessionState.TotalDrops),
                    Theme = theme,
                    TargetCenterX = currentSessionState.TargetCenterX,
                    TargetHalfWidth = currentSessionState.TargetHalfWidth,
                    BullseyeHalfWidth = currentSessionState.BullseyeHalfWidth
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
                trajectory = new
                {
                    startX,
                    startY,
                    landingY,
                    initialVx,
                    initialVy = baseVy,
                    windAccelX,
                    swayAmplitude,
                    swayFrequency,
                    swayPhase,
                    durationSeconds,
                    targetCenterX,
                    targetHalfWidth,
                    bullseyeHalfWidth,
                    landingX = record.LandingX,
                    score = record.Score,
                    isHit = record.IsHit,
                    isBullseye = record.IsBullseye,
                    isWin = record.IsWin,
                    totalTicks = record.TotalTicks,
                    keyframes = record.Keyframes
                }
            };

            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            await QueueWsMessageAsync(WsTopics.Parachute, json, "parachute drop");
            return dropToken;
        }

        public async Task<List<DropletInstanceDto>> SpawnDropletsAsync(DropletSpawnRequest request)
        {
            dropRegistry.CleanupExpiredDrops();
            var settings = await settingsService.GetSettingsAsync();
            var pSettings = settings.Parachute;
            var theme = DropGameMediaResolver.ResolveTheme(settings, null, pSettings.ActiveThemeId);

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
            var dropletImageUrl = await DropGameMediaResolver.ResolveCharacterOrEmoteUrlAsync(emoteService, fakeEventArgs, request.EmoteOrImage, theme.DefaultDropletImageUrl, request.AllowDirectUrlOrPath) ?? "";

            var droplets = new List<DropletInstanceDto>(count);
            var trajectoryUpdates = new List<ParachuteTrajectoryUpdateDto>();
            var now = DateTime.UtcNow;

            lock (_simulationLock)
            {
                var activeParachutes = dropRegistry.GetActiveDrops(DropGameType.Parachute);

                for (var i = 0; i < count; i++)
                {
                    var (sizeLabel, sizeScale, radius, mass) = DropletPatternGenerator.ResolveDropletSize(sizePreset);
                    var (speedLabel, speedScale) = DropletPatternGenerator.ResolveDropletSpeed(speedPreset);
                    var spawnTickOffset = i * 4;

                    var targetDrop = activeParachutes.Count > 0
                        ? activeParachutes[i % activeParachutes.Count]
                        : null;

                    var (startX, startY, vx, vy, gravity) = DropletPatternGenerator.CalculateTrajectory(
                        pattern, i, speedScale, targetDrop, now, spawnTickOffset);

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

                foreach (var drop in activeParachutes)
                {
                    var elapsedTicks = Math.Clamp((int)Math.Round((now - drop.CreatedAtUtc).TotalSeconds * ParachuteSimulationEngine.TickRateHz), 0, drop.TotalTicks - 1);
                    ParachuteSimulationEngine.SimulateParachuteTrajectory(drop, elapsedTicks, droplets);

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

        public async Task ProcessCompletionAsync(PendingDropRecord record)
        {
            var settings = await settingsService.GetSettingsAsync();
            var pSettings = settings.Parachute;

            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["User"] = record.DisplayName,
                ["Target"] = record.DisplayName,
                ["Score"] = record.Score.ToString(CultureInfo.InvariantCulture),
                ["IsWin"] = record.IsWin ? "true" : "false",
                ["IsBullseye"] = record.IsBullseye ? "true" : "false",
                ["DropletHits"] = record.DropletHitCount.ToString(CultureInfo.InvariantCulture),
                ["Theme"] = record.Theme.Name,
                ["Game"] = "ParachuteDrop"
            };

            if (!record.DefaultCommandTriggerExecuted)
            {
                var eventType = record.IsBullseye
                    ? DefaultCommandEventTypes.ParachuteBullseye
                    : (record.IsWin ? DefaultCommandEventTypes.ParachuteWin : DefaultCommandEventTypes.ParachuteLose);

                await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                    "drop",
                    eventType,
                    record.EventArgs,
                    variables);

                record.DefaultCommandTriggerExecuted = true;
            }

            if (record.IsWin)
            {
                if (!record.WinActionExecuted && !string.IsNullOrWhiteSpace(pSettings.WinActionName))
                {
                    await EnqueueConfiguredWinActionAsync(pSettings.WinActionName, record.EventArgs, variables);
                    record.WinActionExecuted = true;
                }

                if (!record.WinChatAnnounced && pSettings.AnnounceWinsInChat && !string.IsNullOrWhiteSpace(pSettings.WinChatMessageTemplate))
                {
                    var bullseyeTag = record.IsBullseye ? " 🎯 BULLSEYE!" : "";
                    var msg = pSettings.WinChatMessageTemplate
                        .Replace("{user}", record.DisplayName, StringComparison.OrdinalIgnoreCase)
                        .Replace("{score}", record.Score.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                        .Replace("{bullseye}", bullseyeTag, StringComparison.OrdinalIgnoreCase)
                        .Replace("{droplethits}", record.DropletHitCount.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
                    await ServiceBackbone.SendChatMessage(msg);
                    record.WinChatAnnounced = true;
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
                    logger.LogWarning("Configured Parachute win action '{ActionName}' was not found or is disabled.", actionName);
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
            var settings = await settingsService.GetSettingsAsync();

            switch (cmdName)
            {
                case "drop":
                    {
                        var pSettings = settings.Parachute;
                        var firstArg = e.Args.Count > 0 ? e.Args[0] : (e.Arg ?? "").Trim();

                        if (e.IsModOrHigher() && firstArg.Equals("start", StringComparison.OrdinalIgnoreCase))
                        {
                            int? customDuration = null;
                            if (e.Args.Count > 1 && int.TryParse(e.Args[1], out var parsedDuration))
                            {
                                customDuration = parsedDuration;
                            }
                            await StartSessionAsync(customDuration, triggeredBy: e.DisplayName);
                            return;
                        }

                        if (e.IsModOrHigher() && firstArg.Equals("stop", StringComparison.OrdinalIgnoreCase))
                        {
                            await StopSessionAsync(triggeredBy: e.DisplayName);
                            return;
                        }

                        if (pSettings.SessionModeEnabled)
                        {
                            var session = GetCurrentSession();
                            if (session == null)
                            {
                                if (pSettings.AutoStartOnCommand)
                                {
                                    var started = await StartSessionAsync(pSettings.DefaultSessionDurationSeconds, triggeredBy: e.DisplayName);
                                    if (started.IsQueued)
                                    {
                                        return;
                                    }
                                }
                                else
                                {
                                    return;
                                }
                            }

                            var state = _activeSession;
                            if (state != null)
                            {
                                var userKey = !string.IsNullOrWhiteSpace(e.UserId) ? e.UserId : (e.Name ?? "viewer");
                                if (pSettings.MaxDropsPerUserPerSession > 0 &&
                                    state.UserDropCounts.TryGetValue(userKey, out var count) &&
                                    count >= pSettings.MaxDropsPerUserPerSession)
                                {
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
            }
        }

        public override async Task Register()
        {
            await RegisterDefaultCommand("drop", this, ModuleName, Rank.Viewer, userCooldown: 10);
            await RegisterDefaultCommand("droplets", this, ModuleName, Rank.Moderator, userCooldown: 5);
        }

        public Task StartAsync(CancellationToken cancellationToken) => Register();

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                var s = Interlocked.Exchange(ref _activeSession, null);
                s?.ExpiryTimer?.Dispose();
                s?.Cts.Cancel();
                s?.Cts.Dispose();
            }
            catch { }
            return Task.CompletedTask;
        }
    }
}
