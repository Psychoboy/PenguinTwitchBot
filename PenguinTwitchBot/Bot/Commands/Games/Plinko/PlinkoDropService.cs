using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Bot.Services.Chat;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Services;

namespace PenguinTwitchBot.Bot.Commands.Games.Plinko
{
    public class PlinkoDropService(
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        IDefaultCommandTriggerService defaultCommandTriggerService,
        IServiceScopeFactory scopeFactory,
        IEmoteService emoteService,
        IWebSocketMessenger webSocketMessenger,
        Application.Notifications.IPenguinDispatcher dispatcher,
        IDropGamesSettingsService settingsService,
        IDropRegistry dropRegistry,
        ILogger<PlinkoDropService> logger,
        IChatColorService? chatColorService = null,
        MediaUploadService? mediaUploadService = null)
        : BaseCommandService(serviceBackbone, commandHandler, "PlinkoDrop", dispatcher), IHostedService, IPlinkoDropService
    {
        private readonly IChatColorService _chatColorService = chatColorService ?? new ChatColorService();
        private readonly MediaUploadService? _mediaUploadService = mediaUploadService;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        private readonly ConcurrentQueue<QueuedSessionRequest> _sessionQueue = new();
        private readonly SemaphoreSlim _sessionLock = new(1, 1);
        private DropGameSessionState? _activeSession;

        private MediaUploadService? GetMediaUploadService()
        {
            if (_mediaUploadService != null) return _mediaUploadService;
            try
            {
                using var scope = scopeFactory.CreateScope();
                return scope.ServiceProvider.GetService<MediaUploadService>();
            }
            catch
            {
                return null;
            }
        }

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
                GameType = "plinko",
                IsActive = true,
                TotalDurationSeconds = state.TotalDurationSeconds,
                RemainingSeconds = remainingSec,
                StartedAtMs = new DateTimeOffset(state.StartedAtUtc).ToUnixTimeMilliseconds(),
                EndsAtMs = new DateTimeOffset(state.EndsAtUtc).ToUnixTimeMilliseconds(),
                TotalDrops = Volatile.Read(ref state.TotalDrops)
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
                    logger.LogInformation("Starting queued Plinko session ({Duration}s, triggered by {User})",
                        next.DurationSeconds, next.TriggeredBy ?? "Queue");
                    await StartSessionAsync(next.DurationSeconds, next.ThemeOverrideId, next.TriggeredBy);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to start queued Plinko session");
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
                : settings.Plinko.DefaultSessionDurationSeconds;

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

                    logger.LogInformation("Enqueued Plinko session for {Duration}s (Triggered by {User}) behind active round. Queue position: {Position}",
                        duration, triggeredBy ?? "System", queuePosition);

                    if (settings.Plinko.AnnounceSessionStatusInChat)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await ServiceBackbone.SendChatMessage($"⏳ Plinko session has been queued behind active round (Queue position: #{queuePosition})!");
                            }
                            catch (Exception ex)
                            {
                                logger.LogDebug(ex, "Failed to send chat announcement for queued session");
                            }
                        });
                    }

                    return new DropGameSessionDto
                    {
                        GameType = "plinko",
                        IsActive = false,
                        IsQueued = true,
                        QueuePosition = queuePosition,
                        TotalDurationSeconds = duration,
                        RemainingSeconds = duration,
                        TotalDrops = 0
                    };
                }

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
                        logger.LogError(ex, "Error expiring session for Plinko");
                    }
                }, null, TimeSpan.FromSeconds(duration), Timeout.InfiniteTimeSpan);

                var theme = DropGameMediaResolver.ResolveTheme(settings, themeOverrideId, settings.Plinko.ActiveThemeId);
                _activeSession = state;
                dropRegistry.ClearDropsForGame(DropGameType.Plinko);
                dropRegistry.SetSessionActive(DropGameType.Plinko, true);
                var isOtherGameActive = dropRegistry.IsSessionActive(DropGameType.Parachute);

                var headerConfig = new DropGameHeaderConfigDto
                {
                    AutoAvoidOverlap = settings.Plinko.AutoAvoidOverlap,
                    ShowBanner = settings.Plinko.ShowHeaderBanner,
                    ShowTimer = settings.Plinko.ShowTimer,
                    ShowCount = settings.Plinko.ShowTokenCount,
                    ShowFreePlayCounter = settings.Plinko.ShowFreePlayCounter,
                    Position = settings.Plinko.HeaderPosition,
                    YOffset = settings.Plinko.HeaderCustomYOffset,
                    NameFontSize = settings.Plinko.NameFontSize
                };

                var dto = new DropGameSessionDto
                {
                    GameType = "plinko",
                    IsActive = true,
                    IsQueued = false,
                    QueuePosition = 0,
                    TotalDurationSeconds = duration,
                    RemainingSeconds = duration,
                    StartedAtMs = new DateTimeOffset(now).ToUnixTimeMilliseconds(),
                    EndsAtMs = new DateTimeOffset(endsAt).ToUnixTimeMilliseconds(),
                    TotalDrops = 0,
                    Theme = theme,
                    IsOtherGameActive = isOtherGameActive,
                    HeaderConfig = headerConfig
                };

                var soundBundle = DropGameMediaResolver.ResolvePlinkoSoundBundle(settings.Plinko, theme, GetMediaUploadService());
                dto.Sounds = soundBundle;

                var startGravity = Math.Clamp(settings.Plinko.Gravity, 0.1, 10.0);
                var startBounciness = Math.Clamp(settings.Plinko.Bounciness, 0.1, 5.0);
                var startMass = Math.Clamp(settings.Plinko.Mass, 0.1, 10.0);
                var startDurationSeconds = Math.Clamp(4.2 / Math.Pow(startGravity, 0.25), 2.5, 7.0);

                var board = new
                {
                    rowCount = Math.Clamp(settings.Plinko.RowCount, 4, 14),
                    pegRadius = Math.Clamp(settings.Plinko.PegRadius, 4, 30),
                    puckRadius = Math.Clamp(settings.Plinko.PuckRadius, 10, 50),
                    bucketHeight = Math.Clamp(settings.Plinko.BucketHeight, 60, 220),
                    durationSeconds = startDurationSeconds,
                    gravity = startGravity,
                    bounciness = startBounciness,
                    mass = startMass,
                    pegTransparency = Math.Clamp(settings.Plinko.PegTransparency, 0, 100),
                    buckets = (settings.Plinko.Buckets.Count > 0 ? settings.Plinko.Buckets : PlinkoGameSettings.CreateDefaultBuckets())
                };

                var wsPayload = JsonSerializer.Serialize(new { type = "session_start", session = dto, board, headerConfig, sounds = soundBundle }, _jsonOptions);
                await QueueWsMessageAsync(WsTopics.Plinko, wsPayload, "Plinko session_start");

                var otherNotice = JsonSerializer.Serialize(new { type = "other_game_status", isOtherGameActive = true, otherGame = "plinko" }, _jsonOptions);
                await QueueWsMessageAsync(WsTopics.Parachute, otherNotice, "Notify Parachute that Plinko is active");

                if (settings.Plinko.AnnounceSessionStatusInChat)
                {
                    var template = settings.Plinko.SessionStartChatMessageTemplate;
                    if (!string.IsNullOrWhiteSpace(template))
                    {
                        var msg = template.Replace("{duration}", duration.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
                        await ServiceBackbone.SendChatMessage(msg);
                    }
                }

                var startVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["User"] = triggeredBy ?? "System",
                    ["Target"] = triggeredBy ?? "System",
                    ["Duration"] = duration.ToString(CultureInfo.InvariantCulture),
                    ["Theme"] = theme.Name,
                    ["TriggeredBy"] = triggeredBy ?? "System",
                    ["Game"] = "Plinko"
                };

                var startEventArgs = new CommandEventArgs
                {
                    Command = "plinko",
                    Name = triggeredBy ?? "System",
                    DisplayName = triggeredBy ?? "System"
                };

                try
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                        "plinko",
                        DefaultCommandEventTypes.PlinkoStart,
                        startEventArgs,
                        startVariables);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to fire PlinkoStart trigger");
                }

                logger.LogInformation("Started Plinko session for {Duration}s (Triggered by {User})", duration, triggeredBy ?? "System");
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
                    logger.LogInformation("Removed {Count} queued Plinko session(s) (Triggered by {User})", removedCount, triggeredBy ?? "System");
                    return new DropGameSessionDto { GameType = "plinko", IsActive = false, RemainingSeconds = 0 };
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
                logger.LogDebug(ex, "Error disposing session timer/cts for Plinko");
            }

            dropRegistry.SetSessionActive(DropGameType.Plinko, false);
            var totalDrops = Volatile.Read(ref state.TotalDrops);
            var settings = await settingsService.GetSettingsAsync();
            var theme = DropGameMediaResolver.ResolveTheme(settings, state.ThemeIdOverride, settings.Plinko.ActiveThemeId);
            var isOtherGameActive = dropRegistry.IsSessionActive(DropGameType.Parachute);

            var headerConfig = new DropGameHeaderConfigDto
            {
                AutoAvoidOverlap = settings.Plinko.AutoAvoidOverlap,
                ShowBanner = settings.Plinko.ShowHeaderBanner,
                ShowTimer = settings.Plinko.ShowTimer,
                ShowCount = settings.Plinko.ShowTokenCount,
                ShowFreePlayCounter = settings.Plinko.ShowFreePlayCounter,
                Position = settings.Plinko.HeaderPosition,
                YOffset = settings.Plinko.HeaderCustomYOffset,
                NameFontSize = settings.Plinko.NameFontSize
            };

            var dto = new DropGameSessionDto
            {
                GameType = "plinko",
                IsActive = false,
                TotalDurationSeconds = state.TotalDurationSeconds,
                RemainingSeconds = 0,
                StartedAtMs = new DateTimeOffset(state.StartedAtUtc).ToUnixTimeMilliseconds(),
                EndsAtMs = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds(),
                TotalDrops = totalDrops,
                Theme = theme,
                IsOtherGameActive = isOtherGameActive,
                HeaderConfig = headerConfig
            };

            var soundBundle = DropGameMediaResolver.ResolvePlinkoSoundBundle(settings.Plinko, theme, GetMediaUploadService());
            dto.Sounds = soundBundle;

            var wsPayload = JsonSerializer.Serialize(new { type = "session_end", session = dto, headerConfig, sounds = soundBundle }, _jsonOptions);
            await QueueWsMessageAsync(WsTopics.Plinko, wsPayload, "Plinko session_end");

            var otherNotice = JsonSerializer.Serialize(new { type = "other_game_status", isOtherGameActive = false, otherGame = "plinko" }, _jsonOptions);
            await QueueWsMessageAsync(WsTopics.Parachute, otherNotice, "Notify Parachute that Plinko is inactive");

            if (!string.Equals(triggeredBy, "restart", StringComparison.OrdinalIgnoreCase))
            {
                if (settings.Plinko.AnnounceSessionStatusInChat)
                {
                    var template = settings.Plinko.SessionEndChatMessageTemplate;
                    if (!string.IsNullOrWhiteSpace(template))
                    {
                        var msg = template
                            .Replace("{count}", totalDrops.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                            .Replace("{duration}", state.TotalDurationSeconds.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
                        await ServiceBackbone.SendChatMessage(msg);
                    }
                }
            }

            logger.LogInformation("Stopped Plinko session with {Drops} total drops (Triggered by {User})", totalDrops, triggeredBy ?? "System");

            if (!string.Equals(triggeredBy, "restart", StringComparison.OrdinalIgnoreCase))
            {
                var endVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["User"] = triggeredBy ?? "System",
                    ["Target"] = triggeredBy ?? "System",
                    ["TotalDrops"] = totalDrops.ToString(CultureInfo.InvariantCulture),
                    ["total_drops"] = totalDrops.ToString(CultureInfo.InvariantCulture),
                    ["TotalTokens"] = totalDrops.ToString(CultureInfo.InvariantCulture),
                    ["Duration"] = state.TotalDurationSeconds.ToString(CultureInfo.InvariantCulture),
                    ["Theme"] = theme.Name,
                    ["TriggeredBy"] = triggeredBy ?? "System",
                    ["Game"] = "PlinkoBounce"
                };

                var endEventArgs = new CommandEventArgs
                {
                    Command = "plinko",
                    Name = triggeredBy ?? "System",
                    DisplayName = triggeredBy ?? "System"
                };

                try
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                        "plinko",
                        DefaultCommandEventTypes.PlinkoEnd,
                        endEventArgs,
                        endVariables);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to fire PlinkoEnd trigger");
                }

                await ProcessNextQueuedSessionAsync();
            }

            return dto;
        }

        public async Task<string> TriggerPlinkoDropAsync(
            CommandEventArgs eventArgs,
            string? emoteOrImage = null,
            string? themeOverrideId = null,
            int? forcedBucketIndex = null,
            bool skipMinimumDurationCheck = false,
            bool allowDirectUrlOrPath = true)
        {
            dropRegistry.CleanupExpiredDrops();
            var settings = await settingsService.GetSettingsAsync();
            var plinkoSettings = settings.Plinko;
            var theme = DropGameMediaResolver.ResolveTheme(settings, themeOverrideId, plinkoSettings.ActiveThemeId);
            var characterImageUrl = await DropGameMediaResolver.ResolveCharacterOrEmoteUrlAsync(
                emoteService, eventArgs, emoteOrImage, theme.DefaultCharacterImageUrl, allowDirectUrlOrPath);
            dropRegistry.RegisterAllowedProxyUrl(characterImageUrl);
            dropRegistry.RegisterAllowedProxyUrl(theme.DefaultCharacterImageUrl);

            var buckets = plinkoSettings.Buckets.Count > 0
                ? plinkoSettings.Buckets
                : PlinkoGameSettings.CreateDefaultBuckets();

            var rows = Math.Clamp(plinkoSettings.RowCount, 4, 14);
            var gravity = Math.Clamp(plinkoSettings.Gravity, 0.1, 10.0);
            var bounciness = Math.Clamp(plinkoSettings.Bounciness, 0.1, 5.0);
            var mass = Math.Clamp(plinkoSettings.Mass, 0.1, 10.0);
            var durationSeconds = Math.Clamp(4.2 / Math.Pow(gravity, 0.25), 2.5, 7.0);
            var pegTransparency = Math.Clamp(plinkoSettings.PegTransparency, 0, 100);

            int bucketIndex = -1;
            string bucketLabel = string.Empty;
            int score = 0;
            bool isWin = false;
            bool isBullseye = false;
            bool isHit = false;

            double startX;
            if (forcedBucketIndex.HasValue)
            {
                var clampedBucket = Math.Clamp(forcedBucketIndex.Value, 0, buckets.Count - 1);
                startX = (clampedBucket + 0.5) / buckets.Count;
                var outcome = PlinkoSimulationEngine.EvaluateBucketOutcome(buckets, clampedBucket);
                bucketIndex = clampedBucket;
                bucketLabel = outcome.Label;
                score = outcome.Score;
                isWin = outcome.IsWin;
                isBullseye = outcome.IsBullseye;
                isHit = outcome.IsWin;
            }
            else
            {
                // All top-row pegs (0 through topPegCount - 1) across the board are eligible for starting above,
                // biased proportionally according to bucket weights so configured weights influence landing outcomes.
                const int pegCols = 14;
                const int topPegCount = pegCols - 1; // 13 pegs across top row

                int pegIndex;
                if (buckets != null && buckets.Count > 0)
                {
                    var weightedBucket = PlinkoSimulationEngine.PickWeightedBucketIndex(buckets);
                    var targetX = (weightedBucket + 0.5) / buckets.Count;
                    var basePeg = (int)Math.Round((targetX * pegCols) - 1.0);
                    var jitter = Random.Shared.Next(-1, 2); // -1, 0, or +1 natural spread
                    pegIndex = Math.Clamp(basePeg + jitter, 0, topPegCount - 1);
                }
                else
                {
                    pegIndex = Random.Shared.Next(0, topPegCount);
                }

                var pegCenterX = (pegIndex + 1.0) / pegCols;
                var microOffset = DropGameMath.RandomDouble(-0.003, 0.003);
                startX = Math.Clamp(pegCenterX + microOffset, 0.06, 0.94);
            }

            var dropToken = RandomNumberGenerator.GetHexString(64);
            var now = DateTime.UtcNow;

            var userColor = !string.IsNullOrWhiteSpace(eventArgs.Color) && eventArgs.Color.StartsWith('#')
                ? eventArgs.Color
                : _chatColorService.GetOrAssignColor(
                    !string.IsNullOrWhiteSpace(eventArgs.UserId) ? eventArgs.UserId : (string.IsNullOrWhiteSpace(eventArgs.Name) ? "viewer" : eventArgs.Name),
                    eventArgs.Color);

            var record = new PendingDropRecord
            {
                DropToken = dropToken,
                GameType = DropGameType.Plinko,
                Username = string.IsNullOrWhiteSpace(eventArgs.Name) ? "viewer" : eventArgs.Name,
                DisplayName = string.IsNullOrWhiteSpace(eventArgs.DisplayName) ? (string.IsNullOrWhiteSpace(eventArgs.Name) ? "Viewer" : eventArgs.Name) : eventArgs.DisplayName,
                UserId = eventArgs.UserId ?? "",
                UserColor = userColor,
                EventArgs = eventArgs,
                Theme = theme,
                CreatedAtUtc = now,
                EarliestCompletionUtc = skipMinimumDurationCheck
                    ? now.AddMilliseconds(-100)
                    : now.AddSeconds(Math.Max(0.5, durationSeconds * 0.50)),
                ExpiresAtUtc = now.AddSeconds(Math.Max(60.0, durationSeconds + 30.0)),
                StartX = startX,
                LandingX = startX,
                TotalTicks = Math.Max(30, (int)Math.Round(durationSeconds * 60.0)),
                Gravity = gravity,
                Bounciness = bounciness,
                Mass = mass,
                Score = score,
                IsHit = isHit,
                IsBullseye = isBullseye,
                IsWin = isWin,
                BucketIndex = bucketIndex,
                BucketLabel = bucketLabel,
                ForcedOutcome = forcedBucketIndex.HasValue,
                Buckets = buckets.Select(b => new PlinkoBucket { Label = b.Label, Score = b.Score, IsWin = b.IsWin, Color = b.Color, Weight = b.Weight }).ToList()
            };

            dropRegistry.RegisterDrop(record);

            var isOtherGameActive = dropRegistry.IsSessionActive(DropGameType.Parachute);
            var headerConfig = new DropGameHeaderConfigDto
            {
                AutoAvoidOverlap = plinkoSettings.AutoAvoidOverlap,
                ShowBanner = plinkoSettings.ShowHeaderBanner,
                ShowTimer = plinkoSettings.ShowTimer,
                ShowCount = plinkoSettings.ShowTokenCount,
                ShowFreePlayCounter = plinkoSettings.ShowFreePlayCounter,
                Position = plinkoSettings.HeaderPosition,
                YOffset = plinkoSettings.HeaderCustomYOffset,
                NameFontSize = plinkoSettings.NameFontSize
            };

            DropGameSessionDto? activeSessionDto = null;
            var userKey = !string.IsNullOrWhiteSpace(eventArgs.UserId) ? eventArgs.UserId : record.Username;
            var state = _activeSession;
            if (state != null && state.EndsAtUtc > now)
            {
                Interlocked.Increment(ref state.TotalDrops);
                state.UserDropCounts.AddOrUpdate(userKey, 1, (_, current) => current + 1);
                var rem = Math.Max(0, (int)Math.Ceiling((state.EndsAtUtc - now).TotalSeconds));
                activeSessionDto = new DropGameSessionDto
                {
                    GameType = "plinko",
                    IsActive = true,
                    TotalDurationSeconds = state.TotalDurationSeconds,
                    RemainingSeconds = rem,
                    StartedAtMs = new DateTimeOffset(state.StartedAtUtc).ToUnixTimeMilliseconds(),
                    EndsAtMs = new DateTimeOffset(state.EndsAtUtc).ToUnixTimeMilliseconds(),
                    TotalDrops = Volatile.Read(ref state.TotalDrops),
                    Theme = theme,
                    IsOtherGameActive = isOtherGameActive,
                    HeaderConfig = headerConfig
                };
            }

            var soundBundle = DropGameMediaResolver.ResolvePlinkoSoundBundle(plinkoSettings, theme, GetMediaUploadService());
            if (activeSessionDto != null)
            {
                activeSessionDto.Sounds = soundBundle;
            }

            var payload = new
            {
                type = "plinko_drop",
                dropToken,
                username = record.Username,
                displayName = record.DisplayName,
                userColor = record.UserColor,
                color = record.UserColor,
                characterImageUrl = characterImageUrl ?? "",
                isCustomEmote = !string.IsNullOrWhiteSpace(characterImageUrl) && !string.Equals(characterImageUrl, theme.DefaultCharacterImageUrl, StringComparison.OrdinalIgnoreCase),
                theme,
                sounds = soundBundle,
                nameFontSize = plinkoSettings.NameFontSize,
                session = activeSessionDto,
                isOtherGameActive,
                headerConfig,
                board = new
                {
                    rowCount = rows,
                    pegRadius = Math.Clamp(plinkoSettings.PegRadius, 4, 30),
                    puckRadius = Math.Clamp(plinkoSettings.PuckRadius, 10, 50),
                    bucketHeight = Math.Clamp(plinkoSettings.BucketHeight, 60, 220),
                    durationSeconds,
                    gravity,
                    bounciness,
                    mass,
                    nameFontSize = plinkoSettings.NameFontSize,
                    pegTransparency,
                    buckets,
                    physics = new
                    {
                        startX,
                        startY = -0.06,
                        gravity,
                        bounciness,
                        mass
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            await QueueWsMessageAsync(WsTopics.Plinko, json, "plinko drop");
            return dropToken;
        }

        public async Task ProcessCompletionAsync(PendingDropRecord record)
        {
            var settings = await settingsService.GetSettingsAsync();
            var plinkoSettings = settings.Plinko;

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

            if (!record.DefaultCommandTriggerExecuted)
            {
                var eventType = record.IsWin
                    ? DefaultCommandEventTypes.PlinkoWin
                    : DefaultCommandEventTypes.PlinkoLose;

                await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                    "plinko",
                    eventType,
                    record.EventArgs,
                    variables);

                record.DefaultCommandTriggerExecuted = true;
            }

            if (record.IsWin)
            {
                if (!record.WinActionExecuted && !string.IsNullOrWhiteSpace(plinkoSettings.WinActionName))
                {
                    await EnqueueConfiguredWinActionAsync(plinkoSettings.WinActionName, record.EventArgs, variables);
                    record.WinActionExecuted = true;
                }

                if (!record.WinChatAnnounced && plinkoSettings.AnnounceWinsInChat && !string.IsNullOrWhiteSpace(plinkoSettings.WinChatMessageTemplate))
                {
                    var template = plinkoSettings.WinChatMessageTemplate;
                    if (record.Score <= 0 && template.Contains("({score} pts)", StringComparison.OrdinalIgnoreCase))
                    {
                        template = template.Replace("({score} pts)", "", StringComparison.OrdinalIgnoreCase).Replace("  ", " ").Trim();
                    }
                    var msg = template
                        .Replace("{user}", record.DisplayName, StringComparison.OrdinalIgnoreCase)
                        .Replace("{score}", record.Score.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                        .Replace("{bucket}", record.BucketLabel, StringComparison.OrdinalIgnoreCase);
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
                logger.LogError(ex, "Failed to broadcast {Description} via WebSocket", description);
            }
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;

            var cmdName = command.CommandProperties.CommandName;
            var settings = await settingsService.GetSettingsAsync();

            if (string.Equals(cmdName, "plinko", StringComparison.OrdinalIgnoreCase))
            {
                var plinkoSettings = settings.Plinko;
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

                if (plinkoSettings.SessionModeEnabled)
                {
                    var session = GetCurrentSession();
                    if (session == null)
                    {
                        if (plinkoSettings.AutoStartOnCommand)
                        {
                            var started = await StartSessionAsync(plinkoSettings.DefaultSessionDurationSeconds, triggeredBy: e.DisplayName);
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
                        if (plinkoSettings.MaxDropsPerUserPerSession > 0 &&
                            state.UserDropCounts.TryGetValue(userKey, out var count) &&
                            count >= plinkoSettings.MaxDropsPerUserPerSession)
                        {
                            return;
                        }
                    }
                }

                await TriggerPlinkoDropAsync(e, e.Arg);
            }
        }

        public override async Task Register()
        {
            await RegisterDefaultCommand("plinko", this, ModuleName, Rank.Viewer, userCooldown: 10);
        }

        public Task StartAsync(CancellationToken cancellationToken) => Register();

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                dropRegistry.SetSessionActive(DropGameType.Plinko, false);
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
