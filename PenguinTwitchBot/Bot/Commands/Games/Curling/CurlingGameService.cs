using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Bot.Services.Chat;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Services;

namespace PenguinTwitchBot.Bot.Commands.Games.Curling
{
    public class CurlingGameService(
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        IDefaultCommandTriggerService defaultCommandTriggerService,
        IServiceScopeFactory scopeFactory,
        IEmoteService emoteService,
        IWebSocketMessenger webSocketMessenger,
        Application.Notifications.IPenguinDispatcher dispatcher,
        IDropGamesSettingsService settingsService,
        IDropRegistry dropRegistry,
        ILogger<CurlingGameService> logger,
        IChatColorService? chatColorService = null,
        MediaUploadService? mediaUploadService = null)
        : BaseCommandService(serviceBackbone, commandHandler, "Curling", dispatcher), IHostedService, ICurlingGameService
    {
        private readonly IChatColorService _chatColorService = chatColorService ?? new ChatColorService();
        private readonly MediaUploadService? _mediaUploadService = mediaUploadService;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        private readonly DropGameSessionManager _sessionManager = new(logger);

        // Dynamic round state for current session
        private CurlingIceCondition _currentSessionCondition = CurlingIceCondition.Normal;
        private double _currentSessionTargetDistance = 84.0;
        private int _slideCounter = 0;

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

        public override async Task Register()
        {
            await RegisterDefaultCommand("curling", this, ModuleName, Rank.Viewer, userCooldown: 10);
            await RegisterDefaultCommand("curl", this, ModuleName, Rank.Viewer, userCooldown: 10);
            await RegisterDefaultCommand("slide", this, ModuleName, Rank.Viewer, userCooldown: 10);
            await RegisterDefaultCommand("shuffle", this, ModuleName, Rank.Viewer, userCooldown: 10);
        }

        public Task StartAsync(CancellationToken cancellationToken) => Register();

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                dropRegistry.SetSessionActive(DropGameType.Curling, false);
                _sessionManager.Dispose();
            }
            catch { }
            return Task.CompletedTask;
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

        public DropGameSessionDto? GetCurrentSession() => _sessionManager.GetCurrentSessionDto("curling");

        public int GetQueuedSessionCount() => _sessionManager.QueuedCount;

        public Task ClearSessionQueueAsync()
        {
            _sessionManager.RemoveQueuedSessions();
            return Task.CompletedTask;
        }

        public async Task<DropGameSessionDto> StartSessionAsync(
            int? durationSeconds = null,
            string? themeOverrideId = null,
            string? triggeredBy = null)
        {
            await _sessionManager.SessionLock.WaitAsync();
            try
            {
                var settings = await settingsService.GetSettingsAsync();
                var cSettings = settings.Curling;
                if (!cSettings.GameEnabled)
                    throw new InvalidOperationException("Curling / Shuffleboard is disabled. Enable it in the game settings first.");

                if (_sessionManager.HasActiveTimedSession())
                {
                    _sessionManager.EnqueueSession(durationSeconds, themeOverrideId, triggeredBy);
                    var queuedDuration = durationSeconds ?? cSettings.DefaultSessionDurationSeconds;
                    return new DropGameSessionDto
                    {
                        GameType = "curling",
                        IsActive = false,
                        IsQueued = true,
                        QueuePosition = _sessionManager.QueuedCount,
                        TotalDurationSeconds = queuedDuration,
                        RemainingSeconds = queuedDuration,
                        TotalDrops = 0
                    };
                }

                var duration = Math.Clamp(durationSeconds ?? cSettings.DefaultSessionDurationSeconds, 10, 600);

                // Roll dynamic sheet conditions and target distance for the new round
                if (cSettings.EnableDynamicIceConditions)
                {
                    var conditions = new[] { CurlingIceCondition.Normal, CurlingIceCondition.Slick, CurlingIceCondition.HeavyFrost, CurlingIceCondition.CrossDrift };
                    _currentSessionCondition = conditions[Random.Shared.Next(conditions.Length)];
                }
                else
                {
                    _currentSessionCondition = cSettings.IceCondition;
                }

                if (cSettings.TargetDistanceMode == CurlingTargetDistanceMode.RandomizePerRound)
                {
                    _currentSessionTargetDistance = Math.Round(70.0 + Random.Shared.NextDouble() * 22.0, 1);
                }
                else
                {
                    _currentSessionTargetDistance = cSettings.TargetDistancePercent;
                }

                var theme = await DropGameMediaResolver.ResolveAndPrepareThemeAsync(settings, themeOverrideId, cSettings.ActiveThemeId, emoteService, dropRegistry);
                var soundBundle = DropGameMediaResolver.ResolveCurlingSoundBundle(cSettings, theme, GetMediaUploadService());
                var headerConfig = DropGameHeaderConfigDto.FromSettings(cSettings);

                var session = _sessionManager.CreateAndSetActiveSession(duration, theme.Id, triggeredBy ?? "System", async _ =>
                {
                    try
                    {
                        await StopSessionAsync("Timer");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error handling Curling session expiration");
                    }
                });

                dropRegistry.SetSessionActive(DropGameType.Curling, true);
                var activeGames = DropGameBroadcastCoordinator.GetActiveGameNames(dropRegistry);
                var isOtherGameActive = activeGames.Any(g => !string.Equals(g, "curling", StringComparison.OrdinalIgnoreCase));

                var dto = GetCurrentSession()!;
                dto.IsOtherGameActive = isOtherGameActive;
                dto.ActiveGames = activeGames;
                dto.HeaderConfig = headerConfig;
                dto.Sounds = soundBundle;

                var conditionLabel = _currentSessionCondition switch
                {
                    CurlingIceCondition.Slick => "Slick Ice",
                    CurlingIceCondition.HeavyFrost => "Heavy Frost",
                    CurlingIceCondition.CrossDrift => "Cross-Drift",
                    _ => "Normal Ice"
                };

                var wsPayload = JsonSerializer.Serialize(new
                {
                    type = "session_start",
                    session = dto,
                    theme,
                    headerConfig,
                    isOtherGameActive,
                    activeGames,
                    sounds = soundBundle,
                    condition = _currentSessionCondition.ToString(),
                    conditionLabel,
                    targetDistancePercent = _currentSessionTargetDistance,
                    boardAlpha = cSettings.BoardAlpha,
                    showBoardBorders = cSettings.ShowBoardBorders,
                    stripPosition = cSettings.StripPosition.ToString(),
                    slideDirection = cSettings.SlideDirection.ToString(),
                    isReversed = (cSettings.SlideDirection is CurlingSlideDirection.RightToLeft or CurlingSlideDirection.BottomToTop),
                    scoringType = cSettings.ScoringType.ToString(),
                    enableShuffleboardKitchenPenalty = cSettings.EnableShuffleboardKitchenPenalty,
                    collisionMode = cSettings.CollisionMode.ToString(),
                    lastCallCutoffSeconds = cSettings.LastCallCutoffSeconds,
                    targetAlpha = cSettings.TargetAlpha,
                    trackThicknessPercent = cSettings.TrackThicknessPercent,
                    powerVariancePercent = cSettings.PowerVariancePercent
                }, _jsonOptions);

                await QueueWsMessageAsync(WsTopics.Curling, wsPayload, "Curling session start");

                // Cross-game notification
                await DropGameBroadcastCoordinator.BroadcastOtherGameStatusAsync(webSocketMessenger, logger, WsTopics.Curling, "curling", isOtherGameActive: true, activeGames: activeGames);

                if (!string.Equals(triggeredBy, "restart", StringComparison.OrdinalIgnoreCase))
                {
                    if (cSettings.AnnounceSessionStatusInChat)
                    {
                        var template = cSettings.SessionStartChatMessageTemplate;
                        if (!string.IsNullOrWhiteSpace(template))
                        {
                            var msg = template
                                .Replace("{duration}", duration.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                                .Replace("{theme}", theme.Name, StringComparison.OrdinalIgnoreCase)
                                .Replace("{condition}", conditionLabel, StringComparison.OrdinalIgnoreCase)
                                .Replace("{distance}", _currentSessionTargetDistance.ToString("F1"), StringComparison.OrdinalIgnoreCase);

                            await ServiceBackbone.SendChatMessage(msg);
                        }
                    }
                }

                var startVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["User"] = triggeredBy ?? "System",
                    ["TriggeredBy"] = triggeredBy ?? "System",
                    ["Duration"] = duration.ToString(CultureInfo.InvariantCulture),
                    ["Theme"] = theme.Name,
                    ["Condition"] = conditionLabel,
                    ["TargetDistance"] = _currentSessionTargetDistance.ToString("F1"),
                    ["Game"] = "Curling"
                };

                var startEventArgs = new CommandEventArgs
                {
                    Command = "curl",
                    Name = triggeredBy ?? "System",
                    DisplayName = triggeredBy ?? "System"
                };

                try
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                        "curl",
                        DefaultCommandEventTypes.CurlingStart,
                        startEventArgs,
                        startVariables);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to fire CurlingStart trigger");
                }

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
                var session = _sessionManager.ExchangeStopSession();
                if (session == null) return null;

                dropRegistry.SetSessionActive(DropGameType.Curling, false);
                var activeGames = DropGameBroadcastCoordinator.GetActiveGameNames(dropRegistry);
                var isOtherGameActive = activeGames.Count > 0;

                var settings = await settingsService.GetSettingsAsync();
                var cSettings = settings.Curling;
                var headerConfig = DropGameHeaderConfigDto.FromSettings(cSettings);

                dto = new DropGameSessionDto
                {
                    GameType = "curling",
                    IsActive = false,
                    TotalDurationSeconds = session.TotalDurationSeconds,
                    RemainingSeconds = 0,
                    StartedAtMs = new DateTimeOffset(session.StartedAtUtc).ToUnixTimeMilliseconds(),
                    EndsAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    TotalDrops = Volatile.Read(ref session.TotalDrops),
                    IsOtherGameActive = isOtherGameActive,
                    ActiveGames = activeGames,
                    HeaderConfig = headerConfig
                };

                var wsPayload = JsonSerializer.Serialize(new
                {
                    type = "session_end",
                    session = dto,
                    headerConfig,
                    isOtherGameActive,
                    activeGames
                }, _jsonOptions);

                await QueueWsMessageAsync(WsTopics.Curling, wsPayload, "Curling session_end");

                await DropGameBroadcastCoordinator.BroadcastOtherGameStatusAsync(webSocketMessenger, logger, WsTopics.Curling, "curling", isOtherGameActive: false, activeGames: activeGames);

                if (!string.Equals(triggeredBy, "restart", StringComparison.OrdinalIgnoreCase))
                {
                    if (cSettings.AnnounceSessionStatusInChat)
                    {
                        var template = cSettings.SessionEndChatMessageTemplate;
                        if (!string.IsNullOrWhiteSpace(template))
                        {
                            var msg = template
                                .Replace("{count}", dto.TotalDrops.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                                .Replace("{duration}", dto.TotalDurationSeconds.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);

                            await ServiceBackbone.SendChatMessage(msg);
                        }
                    }
                }

                var endVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["User"] = triggeredBy ?? "System",
                    ["TotalDrops"] = dto.TotalDrops.ToString(CultureInfo.InvariantCulture),
                    ["total_drops"] = dto.TotalDrops.ToString(CultureInfo.InvariantCulture),
                    ["Duration"] = dto.TotalDurationSeconds.ToString(CultureInfo.InvariantCulture),
                    ["Game"] = "Curling"
                };

                var endEventArgs = new CommandEventArgs
                {
                    Command = "curl",
                    Name = triggeredBy ?? "System",
                    DisplayName = triggeredBy ?? "System"
                };

                try
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                        "curl",
                        DefaultCommandEventTypes.CurlingEnd,
                        endEventArgs,
                        endVariables);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to fire CurlingEnd trigger");
                }
            }
            finally
            {
                _sessionManager.SessionLock.Release();
            }

            if (dto != null)
            {
                await ProcessNextQueuedSessionAsync();
            }

            return dto;
        }

        private async Task ProcessNextQueuedSessionAsync()
        {
            if (_sessionManager.TryDequeueNextQueuedSession(out var nextSession) && nextSession != null)
            {
                try
                {
                    var started = await StartSessionAsync(nextSession.DurationSeconds, nextSession.ThemeOverrideId, nextSession.TriggeredBy);
                    nextSession.CompletionSource?.TrySetResult(started);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to start queued Curling session");
                    nextSession.CompletionSource?.TrySetException(ex);
                }
            }
        }

        public async Task<string> TriggerCurlingSlideAsync(
            CommandEventArgs eventArgs,
            double? power = null,
            string? emoteOrImage = null,
            string? themeOverrideId = null,
            bool skipMinimumDurationCheck = false,
            bool allowDirectUrlOrPath = true,
            CurlingIceCondition? conditionOverride = null,
            double? targetDistanceOverride = null)
        {
            var dropToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            var settings = await settingsService.GetSettingsAsync();
            var cSettings = settings.Curling;
            if (!cSettings.GameEnabled && !_sessionManager.HasActiveTimedSession())
                return string.Empty;

            // Check Last Call Cutoff if session is active
            if (_sessionManager.IsWithinLastCallCutoff(cSettings.LastCallCutoffSeconds, out var remainingSec))
            {
                logger.LogInformation("Curling throw for {User} rejected due to Last Call cutoff ({Remaining:F1}s remaining)", eventArgs.DisplayName, remainingSec);
                return dropToken;
            }

            var condition = conditionOverride ?? (_sessionManager.ActiveSession != null ? _currentSessionCondition : cSettings.IceCondition);
            var targetDistance = targetDistanceOverride ?? (_sessionManager.ActiveSession != null ? _currentSessionTargetDistance : cSettings.TargetDistancePercent);

            // Determine raw power (1-100) and emote token using shared best-effort parser
            var (rawPower, emoteToken) = await DropGameMediaResolver.ParsePowerAndEmoteAsync(
                emoteService,
                eventArgs,
                power,
                emoteOrImage,
                cSettings.EnableViewerPowerControl,
                cSettings.DefaultPowerMin,
                cSettings.DefaultPowerMax);

            var theme = await DropGameMediaResolver.ResolveAndPrepareThemeAsync(settings, themeOverrideId, cSettings.ActiveThemeId, emoteService, dropRegistry);

            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = dropToken,
                GameType = DropGameType.Curling,
                Username = eventArgs.Name ?? "viewer",
                DisplayName = eventArgs.DisplayName ?? eventArgs.Name ?? "Viewer",
                UserId = eventArgs.UserId ?? "",
                EventArgs = eventArgs,
                Theme = theme,
                ConditionLabel = condition switch
                {
                    CurlingIceCondition.Slick => "Slick Ice",
                    CurlingIceCondition.HeavyFrost => "Heavy Frost",
                    CurlingIceCondition.CrossDrift => "Cross-Drift",
                    _ => "Normal Ice"
                },
                TargetDistancePercent = targetDistance,
                Power = rawPower,
                CreatedAtUtc = now,
                EarliestCompletionUtc = now.AddSeconds(1.0),
                ExpiresAtUtc = now.AddSeconds(90.0)
            };

            // Determine direction and reverse
            var isReversed = cSettings.SlideDirection is CurlingSlideDirection.RightToLeft or CurlingSlideDirection.BottomToTop;
            if (cSettings.SlideDirection == CurlingSlideDirection.Alternating)
            {
                isReversed = (Interlocked.Increment(ref _slideCounter) % 2 == 1);
            }
            if (cSettings.SlideDirection == CurlingSlideDirection.CenterTarget)
            {
                targetDistance = 50.0;
                record.TargetDistancePercent = targetDistance;
            }

            // Calculate deterministic physics and rest coordinates
            CurlingSimulationEngine.SimulateCurlingSlide(record, cSettings, rawPower, condition, targetDistance, isReversedOverride: isReversed);

            // Register in server drop registry
            dropRegistry.RegisterDrop(record);

            // Track drop in active session if present
            var userKey = DropGameUserKey.Resolve(eventArgs.UserId, record.Username);
            _sessionManager.RecordUserDrop(userKey);

            // Resolve decal / emote and avatar (fallbackToEventArgsArg: false because ParsePowerAndEmoteAsync already extracted emoteToken)
            var fallbackDecal = !string.IsNullOrWhiteSpace(theme.DefaultCharacterImageUrl)
                ? theme.DefaultCharacterImageUrl
                : theme.CurlingStoneImageUrl;
            var decalUrl = await DropGameMediaResolver.ResolveCharacterOrEmoteUrlAsync(
                emoteService,
                eventArgs,
                emoteToken,
                fallbackUrl: fallbackDecal,
                allowDirectUrlOrPath: allowDirectUrlOrPath,
                fallbackToEventArgsArg: false) ?? "";

            if (string.IsNullOrWhiteSpace(decalUrl) && !string.IsNullOrWhiteSpace(fallbackDecal))
            {
                decalUrl = fallbackDecal;
            }

            dropRegistry.RegisterAllowedProxyUrl(decalUrl);
            dropRegistry.RegisterAllowedProxyUrl(theme.CurlingStoneImageUrl);
            dropRegistry.RegisterAllowedProxyUrl(theme.CurlingHouseImageUrl);
            dropRegistry.RegisterAllowedProxyUrl(theme.DefaultCharacterImageUrl);

            var userColor = !string.IsNullOrWhiteSpace(eventArgs.Color) && eventArgs.Color.StartsWith('#')
                ? eventArgs.Color
                : _chatColorService.GetOrAssignColor(userKey, eventArgs.Color);

            var soundBundle = DropGameMediaResolver.ResolveCurlingSoundBundle(cSettings, theme, _mediaUploadService);
            var activeGames = DropGameBroadcastCoordinator.GetActiveGameNames(dropRegistry);
            var isOtherGameActive = activeGames.Any(g => !string.Equals(g, "curling", StringComparison.OrdinalIgnoreCase));

            var isHorizontal = cSettings.StripPosition is CurlingStripPosition.Bottom or CurlingStripPosition.Top;

            var wsPayload = JsonSerializer.Serialize(new
            {
                type = "curling_slide",
                dropToken,
                username = record.Username,
                displayName = record.DisplayName,
                userColor,
                power = rawPower,
                effectivePower = record.EffectivePower,
                condition = condition.ToString(),
                targetDistancePercent = targetDistance,
                decalUrl,
                theme = theme,
                sounds = soundBundle,
                isOtherGameActive,
                activeGames,
                stripPosition = cSettings.StripPosition.ToString(),
                slideDirection = cSettings.SlideDirection.ToString(),
                isReversed,
                scoringType = cSettings.ScoringType.ToString(),
                enableShuffleboardKitchenPenalty = cSettings.EnableShuffleboardKitchenPenalty,
                collisionMode = cSettings.CollisionMode.ToString(),
                boardAlpha = cSettings.BoardAlpha,
                showBoardBorders = cSettings.ShowBoardBorders,
                targetAlpha = cSettings.TargetAlpha,
                trackThicknessPercent = cSettings.TrackThicknessPercent,
                powerVariancePercent = cSettings.PowerVariancePercent,
                keyframes = record.Keyframes,
                restX = record.LandingX,
                restY = isHorizontal ? 0.50 : record.LandingX,
                totalTicks = record.TotalTicks,
                score = record.Score,
                isBullseye = record.IsBullseye,
                isWin = record.IsWin
            }, _jsonOptions);

            await QueueWsMessageAsync(WsTopics.Curling, wsPayload, $"curling slide for {record.DisplayName}");

            // Notify other games of drop activity
            await DropGameBroadcastCoordinator.BroadcastOtherDropActiveAsync(webSocketMessenger, logger, WsTopics.Curling, durationSeconds: 6.0);

            return dropToken;
        }

        public async Task ProcessCompletionAsync(PendingDropRecord record)
        {
            var actionVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["User"] = record.Username,
                ["DisplayName"] = record.DisplayName,
                ["UserId"] = record.UserId,
                ["Score"] = record.Score.ToString(),
                ["IsWin"] = record.IsWin.ToString().ToLowerInvariant(),
                ["IsBullseye"] = record.IsBullseye.ToString().ToLowerInvariant(),
                ["IsHit"] = record.IsHit.ToString().ToLowerInvariant(),
                ["LandingX"] = record.LandingX.ToString("F4"),
                ["Condition"] = record.ConditionLabel,
                ["IceCondition"] = record.ConditionLabel,
                ["TargetDistance"] = record.TargetDistancePercent.ToString("F1", CultureInfo.InvariantCulture),
                ["TargetDistancePercent"] = record.TargetDistancePercent.ToString("F1", CultureInfo.InvariantCulture),
                ["Power"] = (record.EffectivePower > 0 ? record.EffectivePower : record.Power).ToString("F0", CultureInfo.InvariantCulture),
                ["Theme"] = record.Theme.Name,
                ["Game"] = "Curling"
            };

            // Trigger DefaultCommand events
            try
            {
                if (record.IsBullseye)
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync("curl", DefaultCommandEventTypes.CurlingBullseye, record.EventArgs, actionVars);
                }

                if (record.IsWin || record.IsBullseye)
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync("curl", DefaultCommandEventTypes.CurlingWin, record.EventArgs, actionVars);
                }
                else
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync("curl", DefaultCommandEventTypes.CurlingLose, record.EventArgs, actionVars);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fire Curling DefaultCommandEvent for {User}", record.DisplayName);
            }

        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;

            var cmdName = command.CommandProperties.CommandName;
            var settings = await settingsService.GetSettingsAsync();
            var cSettings = settings.Curling;

            if (cmdName.Equals("curling", StringComparison.OrdinalIgnoreCase) ||
                cmdName.Equals("curl", StringComparison.OrdinalIgnoreCase) ||
                cmdName.Equals("slide", StringComparison.OrdinalIgnoreCase) ||
                cmdName.Equals("shuffle", StringComparison.OrdinalIgnoreCase))
            {
                var firstArg = e.Args.Count > 0 ? e.Args[0] : (e.Arg ?? "").Trim();

                if (e.IsModOrHigher() && firstArg.Equals("start", StringComparison.OrdinalIgnoreCase))
                {
                    int? customDuration = null;
                    if (e.Args.Count > 1 && int.TryParse(e.Args[1], out var parsedDuration))
                    {
                        customDuration = parsedDuration;
                    }
                    if (!cSettings.GameEnabled)
                    {
                        logger.LogInformation("Ignored !curling start from {User} because Curling is disabled", e.DisplayName);
                        return;
                    }
                    await StartSessionAsync(customDuration, triggeredBy: e.DisplayName);
                    return;
                }

                if (e.IsModOrHigher() && firstArg.Equals("stop", StringComparison.OrdinalIgnoreCase))
                {
                    await StopSessionAsync(triggeredBy: e.DisplayName);
                    return;
                }

                if (!cSettings.GameEnabled && !_sessionManager.HasActiveTimedSession())
                {
                    logger.LogInformation("Ignored !curling from {User} because Curling is disabled", e.DisplayName);
                    return;
                }

                if (cSettings.SessionModeEnabled)
                {
                    var session = GetCurrentSession();
                    if (session == null)
                    {
                        if (cSettings.AutoStartOnCommand)
                        {
                            var started = await StartSessionAsync(cSettings.DefaultSessionDurationSeconds, triggeredBy: e.DisplayName);
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

                    var userKey = DropGameUserKey.Resolve(e.UserId, e.Name);
                    if (!_sessionManager.CanUserDrop(userKey, cSettings.MaxDropsPerUserPerSession, out _, out _))
                    {
                        return;
                    }
                }

                await TriggerCurlingSlideAsync(e, allowDirectUrlOrPath: false);
            }
        }
    }
}
