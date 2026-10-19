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
        private readonly DropGameSessionManager _sessionManager = new(logger);

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

        public DropGameSessionDto? GetCurrentSession() => _sessionManager.GetCurrentSessionDto("plinko");

        public int GetQueuedSessionCount() => _sessionManager.QueuedCount;

        public Task ClearSessionQueueAsync()
        {
            _sessionManager.RemoveQueuedSessions();
            return Task.CompletedTask;
        }

        private async Task ProcessNextQueuedSessionAsync()
        {
            QueuedSessionRequest? next = null;
            await _sessionManager.SessionLock.WaitAsync();
            try
            {
                if (_sessionManager.HasActiveTimedSession()) return;
                if (_sessionManager.TryDequeueNextQueuedSession(out var req))
                {
                    next = req;
                }
            }
            finally
            {
                _sessionManager.SessionLock.Release();
            }

            if (next != null)
            {
                try
                {
                    logger.LogInformation("Starting queued Plinko session ({Duration}s, triggered by {User})",
                        next.DurationSeconds, next.TriggeredBy ?? "Queue");
                    var dto = await StartSessionAsync(next.DurationSeconds, next.ThemeOverrideId, next.TriggeredBy);
                    next.CompletionSource?.TrySetResult(dto);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to start queued Plinko session");
                    next.CompletionSource?.TrySetException(ex);
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

            await _sessionManager.SessionLock.WaitAsync();
            try
            {
                if (_sessionManager.HasActiveTimedSession())
                {
                    _sessionManager.EnqueueSession(duration, themeOverrideId, triggeredBy);
                    var queuePosition = _sessionManager.QueuedCount;

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

                var session = _sessionManager.CreateAndSetActiveSession(duration, themeOverrideId, triggeredBy ?? "System", async state =>
                {
                    try
                    {
                        await StopSessionAsync("timer_expired");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error expiring session for Plinko");
                    }
                });

                var theme = DropGameMediaResolver.ResolveTheme(settings, themeOverrideId, settings.Plinko.ActiveThemeId);
                dropRegistry.ClearDropsForGame(DropGameType.Plinko);
                dropRegistry.SetSessionActive(DropGameType.Plinko, true);
                var activeGames = DropGameBroadcastCoordinator.GetActiveGameNames(dropRegistry);
                var isOtherGameActive = activeGames.Any(g => !string.Equals(g, "plinko", StringComparison.OrdinalIgnoreCase));

                var headerConfig = DropGameHeaderConfigDto.FromSettings(settings.Plinko);

                var dto = new DropGameSessionDto
                {
                    GameType = "plinko",
                    IsActive = true,
                    IsQueued = false,
                    QueuePosition = 0,
                    TotalDurationSeconds = duration,
                    RemainingSeconds = duration,
                    StartedAtMs = new DateTimeOffset(session.StartedAtUtc).ToUnixTimeMilliseconds(),
                    EndsAtMs = new DateTimeOffset(session.EndsAtUtc).ToUnixTimeMilliseconds(),
                    TotalDrops = 0,
                    Theme = theme,
                    IsOtherGameActive = isOtherGameActive,
                    ActiveGames = activeGames,
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

                var wsPayload = JsonSerializer.Serialize(new { type = "session_start", session = dto, board, headerConfig, sounds = soundBundle, isOtherGameActive, activeGames }, _jsonOptions);
                await QueueWsMessageAsync(WsTopics.Plinko, wsPayload, "Plinko session_start");

                await DropGameBroadcastCoordinator.BroadcastOtherGameStatusAsync(webSocketMessenger, logger, WsTopics.Plinko, "plinko", isOtherGameActive: true, activeGames: activeGames);

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
                _sessionManager.SessionLock.Release();
            }
        }

        public async Task<DropGameSessionDto?> StopSessionAsync(string? triggeredBy = null)
        {
            DropGameSessionDto? dto = null;
            await _sessionManager.SessionLock.WaitAsync();
            try
            {
                var state = _sessionManager.ExchangeStopSession();
                if (state == null)
                {
                    var removedCount = _sessionManager.RemoveQueuedSessions();
                    if (removedCount > 0)
                    {
                        logger.LogInformation("Removed {Count} queued Plinko session(s) (Triggered by {User})", removedCount, triggeredBy ?? "System");
                        return new DropGameSessionDto { GameType = "plinko", IsActive = false, RemainingSeconds = 0 };
                    }
                    return null;
                }

                dropRegistry.SetSessionActive(DropGameType.Plinko, false);
                var activeGames = DropGameBroadcastCoordinator.GetActiveGameNames(dropRegistry);
                var isOtherGameActive = activeGames.Count > 0;
                var totalDrops = Volatile.Read(ref state.TotalDrops);
                var settings = await settingsService.GetSettingsAsync();
                var theme = DropGameMediaResolver.ResolveTheme(settings, state.ThemeIdOverride, settings.Plinko.ActiveThemeId);

                var headerConfig = DropGameHeaderConfigDto.FromSettings(settings.Plinko);

                dto = new DropGameSessionDto
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
                    ActiveGames = activeGames,
                    HeaderConfig = headerConfig
                };

                var soundBundle = DropGameMediaResolver.ResolvePlinkoSoundBundle(settings.Plinko, theme, GetMediaUploadService());
                dto.Sounds = soundBundle;

                var wsPayload = JsonSerializer.Serialize(new { type = "session_end", session = dto, headerConfig, sounds = soundBundle, isOtherGameActive, activeGames }, _jsonOptions);
                await QueueWsMessageAsync(WsTopics.Plinko, wsPayload, "Plinko session_end");

                await DropGameBroadcastCoordinator.BroadcastOtherGameStatusAsync(webSocketMessenger, logger, WsTopics.Plinko, "plinko", isOtherGameActive: false, activeGames: activeGames);

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
                }
            }
            finally
            {
                _sessionManager.SessionLock.Release();
            }

            if (dto != null && !string.Equals(triggeredBy, "restart", StringComparison.OrdinalIgnoreCase))
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

            var buckets = plinkoSettings.Buckets is { Count: > 0 }
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
                if (buckets.Count > 0)
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

            var isOtherGameActive = dropRegistry.IsSessionActive(DropGameType.Parachute) || dropRegistry.IsSessionActive(DropGameType.Balloon);
            var headerConfig = DropGameHeaderConfigDto.FromSettings(plinkoSettings);

            DropGameSessionDto? activeSessionDto = null;
            var userKey = !string.IsNullOrWhiteSpace(eventArgs.UserId) ? eventArgs.UserId : record.Username;
            var state = _sessionManager.ActiveSession;
            if (state != null && state.EndsAtUtc > now)
            {
                _sessionManager.RecordUserDrop(userKey);
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

            await DropGameBroadcastCoordinator.BroadcastOtherDropActiveAsync(webSocketMessenger, logger, WsTopics.Plinko, durationSeconds: 12.0);

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

                    var userKey = !string.IsNullOrWhiteSpace(e.UserId) ? e.UserId : (e.Name ?? "viewer");
                    if (!_sessionManager.CanUserDrop(userKey, plinkoSettings.MaxDropsPerUserPerSession))
                    {
                        return;
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
                _sessionManager.Dispose();
            }
            catch { }
            return Task.CompletedTask;
        }
    }
}
