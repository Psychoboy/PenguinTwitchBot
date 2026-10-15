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
        ILogger<PlinkoDropService> logger)
        : BaseCommandService(serviceBackbone, commandHandler, "PlinkoDrop", dispatcher), IHostedService, IPlinkoDropService
    {
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        private readonly ConcurrentQueue<QueuedSessionRequest> _sessionQueue = new();
        private readonly SemaphoreSlim _sessionLock = new(1, 1);
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
                    Theme = theme
                };

                var board = new
                {
                    rowCount = Math.Clamp(settings.Plinko.RowCount, 5, 14),
                    durationSeconds = Math.Clamp(settings.Plinko.DropDurationSeconds, 2.0, 12.0),
                    pegTransparency = Math.Clamp(settings.Plinko.PegTransparency, 0, 100),
                    buckets = (settings.Plinko.Buckets.Count > 0 ? settings.Plinko.Buckets : PlinkoGameSettings.CreateDefaultBuckets())
                };

                var wsPayload = JsonSerializer.Serialize(new { type = "session_start", session = dto, board }, _jsonOptions);
                await QueueWsMessageAsync(WsTopics.Plinko, wsPayload, "Plinko session_start");

                if (settings.Plinko.AnnounceSessionStatusInChat)
                {
                    var template = settings.Plinko.SessionStartChatMessageTemplate;
                    if (!string.IsNullOrWhiteSpace(template))
                    {
                        var msg = template.Replace("{duration}", duration.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
                        await ServiceBackbone.SendChatMessage(msg);
                    }
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

            var totalDrops = Volatile.Read(ref state.TotalDrops);
            var settings = await settingsService.GetSettingsAsync();
            var theme = DropGameMediaResolver.ResolveTheme(settings, state.ThemeIdOverride, settings.Plinko.ActiveThemeId);

            var dto = new DropGameSessionDto
            {
                GameType = "plinko",
                IsActive = false,
                TotalDurationSeconds = state.TotalDurationSeconds,
                RemainingSeconds = 0,
                StartedAtMs = new DateTimeOffset(state.StartedAtUtc).ToUnixTimeMilliseconds(),
                EndsAtMs = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds(),
                TotalDrops = totalDrops,
                Theme = theme
            };

            var wsPayload = JsonSerializer.Serialize(new { type = "session_end", session = dto }, _jsonOptions);
            await QueueWsMessageAsync(WsTopics.Plinko, wsPayload, "Plinko session_end");

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
            bool allowDirectUrlOrPath = false)
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

            var rows = Math.Clamp(plinkoSettings.RowCount, 5, 14);
            var durationSeconds = Math.Clamp(plinkoSettings.DropDurationSeconds, 2.0, 12.0);
            var pegTransparency = Math.Clamp(plinkoSettings.PegTransparency, 0, 100);

            int winningBucketIndex;
            if (forcedBucketIndex.HasValue)
            {
                winningBucketIndex = Math.Clamp(forcedBucketIndex.Value, 0, buckets.Count - 1);
            }
            else
            {
                winningBucketIndex = PlinkoSimulationEngine.PickWeightedBucketIndex(buckets);
            }

            var winningBucket = buckets[winningBucketIndex];
            var effectiveScore = winningBucket.IsWin ? winningBucket.Score : 0;
            var waypoints = PlinkoSimulationEngine.BuildPlinkoWaypoints(rows, buckets.Count, winningBucketIndex);

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
                BucketLabel = winningBucket.Label,
                Buckets = buckets.Select(b => new PlinkoBucket { Label = b.Label, Score = b.Score, IsWin = b.IsWin, Color = b.Color, Weight = b.Weight }).ToList()
            };

            dropRegistry.RegisterDrop(record);

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
                    pegTransparency,
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
                    var msg = plinkoSettings.WinChatMessageTemplate
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
                throw;
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
