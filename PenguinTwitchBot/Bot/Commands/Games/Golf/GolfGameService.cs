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

namespace PenguinTwitchBot.Bot.Commands.Games.Golf
{
    public class GolfGameService(
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        IDefaultCommandTriggerService defaultCommandTriggerService,
        IServiceScopeFactory scopeFactory,
        IEmoteService emoteService,
        IWebSocketMessenger webSocketMessenger,
        Application.Notifications.IPenguinDispatcher dispatcher,
        IDropGamesSettingsService settingsService,
        IDropRegistry dropRegistry,
        ILogger<GolfGameService> logger,
        IChatColorService? chatColorService = null,
        MediaUploadService? mediaUploadService = null)
        : BaseCommandService(serviceBackbone, commandHandler, "Golf", dispatcher), IHostedService, IGolfGameService
    {
        private readonly IChatColorService _chatColorService = chatColorService ?? new ChatColorService();
        private readonly MediaUploadService? _mediaUploadService = mediaUploadService;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        private readonly DropGameSessionManager _sessionManager = new(logger);

        // Per-player multi-stroke state within the current round/free-play window
        private readonly ConcurrentDictionary<string, PlayerGolfRoundState> _playerRoundStates = new(StringComparer.OrdinalIgnoreCase);

        // Dynamic round state for current session
        private GolfGreenCondition _currentSessionCondition = GolfGreenCondition.StandardGreen;
        private double _currentSessionTargetDistance = 84.0;
        private int _shotCounter = 0;

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
            await RegisterDefaultCommand("golf", this, ModuleName, Rank.Viewer, userCooldown: 5);
            await RegisterDefaultCommand("putt", this, ModuleName, Rank.Viewer, userCooldown: 5);
            await RegisterDefaultCommand("chip", this, ModuleName, Rank.Viewer, userCooldown: 5);
            await RegisterDefaultCommand("swing", this, ModuleName, Rank.Viewer, userCooldown: 5);
        }

        public Task StartAsync(CancellationToken cancellationToken) => Register();

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                dropRegistry.SetSessionActive(DropGameType.Golf, false);
                _playerRoundStates.Clear();
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

        public DropGameSessionDto? GetCurrentSession() => _sessionManager.GetCurrentSessionDto("golf");

        public int GetQueuedSessionCount() => _sessionManager.QueuedCount;

        public Task ClearSessionQueueAsync()
        {
            _sessionManager.RemoveQueuedSessions();
            return Task.CompletedTask;
        }

        public static string FormatConditionLabel(GolfGreenCondition condition) => condition switch
        {
            GolfGreenCondition.FastLinks => "Fast Links",
            GolfGreenCondition.HeavyDew => "Heavy Dew",
            GolfGreenCondition.CrossBreeze => "Cross-Breeze",
            _ => "Standard Green"
        };

        public async Task<DropGameSessionDto> StartSessionAsync(
            int? durationSeconds = null,
            string? themeOverrideId = null,
            string? triggeredBy = null)
        {
            await _sessionManager.SessionLock.WaitAsync();
            try
            {
                var settings = await settingsService.GetSettingsAsync();
                var gSettings = settings.Golf;

                if (_sessionManager.HasActiveTimedSession())
                {
                    _sessionManager.EnqueueSession(durationSeconds, themeOverrideId, triggeredBy);
                    var queuedDuration = durationSeconds ?? gSettings.DefaultSessionDurationSeconds;
                    return new DropGameSessionDto
                    {
                        GameType = "golf",
                        IsActive = false,
                        IsQueued = true,
                        QueuePosition = _sessionManager.QueuedCount,
                        TotalDurationSeconds = queuedDuration,
                        RemainingSeconds = queuedDuration,
                        TotalDrops = 0
                    };
                }

                _playerRoundStates.Clear();

                var duration = Math.Clamp(durationSeconds ?? gSettings.DefaultSessionDurationSeconds, 10, 600);

                // Roll dynamic green conditions and pin distance for the new round
                if (gSettings.EnableDynamicGreenConditions)
                {
                    var conditions = new[]
                    {
                        GolfGreenCondition.StandardGreen,
                        GolfGreenCondition.FastLinks,
                        GolfGreenCondition.HeavyDew,
                        GolfGreenCondition.CrossBreeze
                    };
                    _currentSessionCondition = conditions[Random.Shared.Next(conditions.Length)];
                }
                else
                {
                    _currentSessionCondition = gSettings.GreenCondition;
                }

                if (gSettings.TargetDistanceMode == GolfPinDistanceMode.RandomizePerRound)
                {
                    _currentSessionTargetDistance = Math.Round(70.0 + Random.Shared.NextDouble() * 22.0, 1);
                }
                else
                {
                    _currentSessionTargetDistance = gSettings.TargetDistancePercent;
                }

                var theme = DropGameMediaResolver.ResolveTheme(settings, themeOverrideId, gSettings.ActiveThemeId);
                var soundBundle = DropGameMediaResolver.ResolveGolfSoundBundle(gSettings, theme, GetMediaUploadService());
                var headerConfig = DropGameHeaderConfigDto.FromSettings(gSettings);

                _sessionManager.CreateAndSetActiveSession(duration, theme.Id, triggeredBy ?? "System", async _ =>
                {
                    try
                    {
                        await StopSessionAsync("Timer");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error handling Golf session expiration");
                    }
                });

                dropRegistry.SetSessionActive(DropGameType.Golf, true);
                var activeGames = DropGameBroadcastCoordinator.GetActiveGameNames(dropRegistry);
                var isOtherGameActive = activeGames.Any(g => !string.Equals(g, "golf", StringComparison.OrdinalIgnoreCase));

                var dto = GetCurrentSession()!;
                dto.IsOtherGameActive = isOtherGameActive;
                dto.ActiveGames = activeGames;
                dto.HeaderConfig = headerConfig;
                dto.Sounds = soundBundle;

                var conditionLabel = FormatConditionLabel(_currentSessionCondition);

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
                    boardAlpha = gSettings.BoardAlpha,
                    showBoardBorders = gSettings.ShowBoardBorders,
                    stripPosition = gSettings.StripPosition.ToString(),
                    slideDirection = gSettings.SlideDirection.ToString(),
                    isReversed = (gSettings.SlideDirection is CurlingSlideDirection.RightToLeft or CurlingSlideDirection.BottomToTop),
                    collisionMode = gSettings.CollisionMode.ToString(),
                    lastCallCutoffSeconds = gSettings.LastCallCutoffSeconds,
                    targetAlpha = gSettings.TargetAlpha,
                    trackThicknessPercent = gSettings.TrackThicknessPercent,
                    powerVariancePercent = gSettings.PowerVariancePercent,
                    cupCaptureSpeedMultiplier = gSettings.CupCaptureSpeedMultiplier,
                    maxStrokesPerPlayer = gSettings.MaxStrokesPerPlayer,
                    strokePenaltyPoints = gSettings.StrokePenaltyPoints,
                    maxScore = gSettings.MaxScore
                }, _jsonOptions);

                await QueueWsMessageAsync(WsTopics.Golf, wsPayload, "Golf session start");

                // Cross-game notification
                await DropGameBroadcastCoordinator.BroadcastOtherGameStatusAsync(
                    webSocketMessenger,
                    logger,
                    WsTopics.Golf,
                    "golf",
                    isOtherGameActive: true,
                    activeGames: activeGames);

                if (!string.Equals(triggeredBy, "restart", StringComparison.OrdinalIgnoreCase))
                {
                    if (gSettings.AnnounceSessionStatusInChat)
                    {
                        var template = gSettings.SessionStartChatMessageTemplate;
                        if (!string.IsNullOrWhiteSpace(template))
                        {
                            var msg = template
                                .Replace("{duration}", duration.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                                .Replace("{theme}", theme.Name, StringComparison.OrdinalIgnoreCase)
                                .Replace("{condition}", conditionLabel, StringComparison.OrdinalIgnoreCase)
                                .Replace("{distance}", _currentSessionTargetDistance.ToString("F0", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);

                            await ServiceBackbone.SendChatMessage(msg);
                        }
                    }
                }

                var startVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["User"] = triggeredBy ?? "System",
                    ["Duration"] = duration.ToString(CultureInfo.InvariantCulture),
                    ["Theme"] = theme.Name,
                    ["Condition"] = conditionLabel,
                    ["TargetDistance"] = _currentSessionTargetDistance.ToString("F1", CultureInfo.InvariantCulture),
                    ["Game"] = "Golf"
                };

                var startEventArgs = new CommandEventArgs
                {
                    Command = "golf",
                    Name = triggeredBy ?? "System",
                    DisplayName = triggeredBy ?? "System"
                };

                try
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                        "golf",
                        DefaultCommandEventTypes.GolfStart,
                        startEventArgs,
                        startVariables);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to fire GolfStart trigger");
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

                // Multi-stroke: players whose round ended unfinished (strokes left, not sunk) get their last stroke's outcome now
                var deferredRecords = new List<PendingDropRecord>();
                foreach (var pState in _playerRoundStates.Values)
                {
                    lock (pState)
                    {
                        if (pState.DeferredOutcomeRecord != null)
                        {
                            deferredRecords.Add(pState.DeferredOutcomeRecord);
                            pState.DeferredOutcomeRecord = null;
                        }
                    }
                }

                _playerRoundStates.Clear();

                foreach (var deferred in deferredRecords)
                {
                    await FireOutcomeAsync(deferred);
                }

                dropRegistry.SetSessionActive(DropGameType.Golf, false);
                var activeGames = DropGameBroadcastCoordinator.GetActiveGameNames(dropRegistry);
                var isOtherGameActive = activeGames.Count > 0;

                var settings = await settingsService.GetSettingsAsync();
                var gSettings = settings.Golf;
                var headerConfig = DropGameHeaderConfigDto.FromSettings(gSettings);

                dto = new DropGameSessionDto
                {
                    GameType = "golf",
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

                await QueueWsMessageAsync(WsTopics.Golf, wsPayload, "Golf session_end");

                await DropGameBroadcastCoordinator.BroadcastOtherGameStatusAsync(
                    webSocketMessenger,
                    logger,
                    WsTopics.Golf,
                    "golf",
                    isOtherGameActive: false,
                    activeGames: activeGames);

                if (!string.Equals(triggeredBy, "restart", StringComparison.OrdinalIgnoreCase))
                {
                    if (gSettings.AnnounceSessionStatusInChat)
                    {
                        var template = gSettings.SessionEndChatMessageTemplate;
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
                    ["Game"] = "Golf"
                };

                var endEventArgs = new CommandEventArgs
                {
                    Command = "golf",
                    Name = triggeredBy ?? "System",
                    DisplayName = triggeredBy ?? "System"
                };

                try
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                        "golf",
                        DefaultCommandEventTypes.GolfEnd,
                        endEventArgs,
                        endVariables);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to fire GolfEnd trigger");
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
                    logger.LogError(ex, "Failed to start queued Golf session");
                    nextSession.CompletionSource?.TrySetException(ex);
                }
            }
        }

        public async Task<string> TriggerGolfShotAsync(
            CommandEventArgs eventArgs,
            double? power = null,
            string? emoteOrImage = null,
            string? themeOverrideId = null,
            bool skipMinimumDurationCheck = false,
            bool allowDirectUrlOrPath = true,
            GolfGreenCondition? conditionOverride = null,
            double? targetDistanceOverride = null)
        {
            var dropToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            var settings = await settingsService.GetSettingsAsync();
            var gSettings = settings.Golf;

            // Check Last Call Cutoff if session is active
            if (_sessionManager.IsWithinLastCallCutoff(gSettings.LastCallCutoffSeconds, out var remainingSec))
            {
                logger.LogInformation("Golf shot for {User} rejected due to Last Call cutoff ({Remaining:F1}s remaining)", eventArgs.DisplayName, remainingSec);
                return dropToken;
            }

            var condition = conditionOverride ?? (_sessionManager.ActiveSession != null ? _currentSessionCondition : gSettings.GreenCondition);
            var targetDistance = targetDistanceOverride ?? (_sessionManager.ActiveSession != null ? _currentSessionTargetDistance : gSettings.TargetDistancePercent);

            // Determine raw power (1-100) and emote token using shared best-effort parser
            var (rawPower, emoteToken) = await DropGameMediaResolver.ParsePowerAndEmoteAsync(
                emoteService,
                eventArgs,
                power,
                emoteOrImage,
                gSettings.EnableViewerPowerControl,
                gSettings.DefaultPowerMin,
                gSettings.DefaultPowerMax);

            var theme = DropGameMediaResolver.ResolveTheme(settings, themeOverrideId, gSettings.ActiveThemeId);
            var userKey = DropGameUserKey.Resolve(eventArgs.UserId, eventArgs.Name);

            // Determine direction and reverse
            var isReversed = gSettings.SlideDirection is CurlingSlideDirection.RightToLeft or CurlingSlideDirection.BottomToTop;
            if (gSettings.SlideDirection == CurlingSlideDirection.Alternating)
            {
                isReversed = (Interlocked.Increment(ref _shotCounter) % 2 == 1);
            }
            if (gSettings.SlideDirection == CurlingSlideDirection.CenterTarget)
            {
                targetDistance = 50.0;
            }

            // Multi-stroke tracking: determine stroke number and starting ball lie
            var maxStrokes = Math.Clamp(gSettings.MaxStrokesPerPlayer, 1, 5);
            int strokeNumber = 1;
            double? startAxisOverride = null;

            if (maxStrokes > 1)
            {
                var pState = _playerRoundStates.GetOrAdd(userKey, _ => new PlayerGolfRoundState());
                lock (pState)
                {
                    if (pState.IsSunk || pState.StrokesUsed >= maxStrokes)
                    {
                        // If session mode is not active (free play), reset for a brand new hole once finished
                        if (_sessionManager.ActiveSession == null)
                        {
                            pState.StrokesUsed = 0;
                            pState.IsSunk = false;
                            pState.CurrentAxisPos = 0.05;
                        }
                        else
                        {
                            return dropToken;
                        }
                    }

                    strokeNumber = pState.StrokesUsed + 1;
                    if (strokeNumber > 1)
                    {
                        // If previous shot went completely out-of-bounds (clamped to extreme edge), re-tee
                        if (pState.CurrentAxisPos is <= 0.025 or >= 0.975)
                        {
                            startAxisOverride = 0.05;
                        }
                        else
                        {
                            startAxisOverride = pState.CurrentAxisPos;
                        }
                    }
                    pState.StrokesUsed = strokeNumber;
                    pState.LastDropToken = dropToken;
                }
            }

            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = dropToken,
                GameType = DropGameType.Golf,
                Username = eventArgs.Name ?? "viewer",
                DisplayName = eventArgs.DisplayName ?? eventArgs.Name ?? "Viewer",
                UserId = eventArgs.UserId ?? "",
                StrokeNumber = strokeNumber,
                EventArgs = eventArgs,
                Theme = theme,
                // Distance from the tee; the overlay mirrors it when reversed.
                StartAxisPosition = startAxisOverride ?? 0.05,
                CreatedAtUtc = now,
                EarliestCompletionUtc = now.AddSeconds(0.8),
                ExpiresAtUtc = now.AddSeconds(90.0)
            };

            // Register in server drop registry
            dropRegistry.RegisterDrop(record);

            // Track drop in active session if present (only count a new player entry on Stroke 1 when MaxStrokesPerPlayer > 1)
            if (strokeNumber == 1)
            {
                _sessionManager.RecordUserDrop(userKey);
            }

            // Resolve ball decal / emote (fallbackToEventArgsArg: false because ParsePowerAndEmoteAsync already extracted emoteToken)
            var fallbackDecal = !string.IsNullOrWhiteSpace(theme.DefaultCharacterImageUrl)
                ? theme.DefaultCharacterImageUrl
                : theme.GolfBallImageUrl;
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
            dropRegistry.RegisterAllowedProxyUrl(theme.GolfBallImageUrl);
            dropRegistry.RegisterAllowedProxyUrl(theme.GolfGreenImageUrl);
            dropRegistry.RegisterAllowedProxyUrl(theme.GolfFlagPinImageUrl);
            dropRegistry.RegisterAllowedProxyUrl(theme.DefaultCharacterImageUrl);

            var userColor = !string.IsNullOrWhiteSpace(eventArgs.Color) && eventArgs.Color.StartsWith('#')
                ? eventArgs.Color
                : _chatColorService.GetOrAssignColor(userKey, eventArgs.Color);

            var soundBundle = DropGameMediaResolver.ResolveGolfSoundBundle(gSettings, theme, _mediaUploadService);
            var activeGames = DropGameBroadcastCoordinator.GetActiveGameNames(dropRegistry);
            var isOtherGameActive = activeGames.Any(g => !string.Equals(g, "golf", StringComparison.OrdinalIgnoreCase));

            var wsPayload = JsonSerializer.Serialize(new
            {
                type = "golf_shot",
                dropToken,
                username = record.Username,
                displayName = record.DisplayName,
                userId = record.UserId,
                userColor,
                power = rawPower,
                condition = condition.ToString(),
                conditionLabel = FormatConditionLabel(condition),
                targetDistancePercent = targetDistance,
                decalUrl,
                theme,
                sounds = soundBundle,
                isOtherGameActive,
                activeGames,
                stripPosition = gSettings.StripPosition.ToString(),
                slideDirection = gSettings.SlideDirection.ToString(),
                isReversed,
                collisionMode = gSettings.CollisionMode.ToString(),
                boardAlpha = gSettings.BoardAlpha,
                showBoardBorders = gSettings.ShowBoardBorders,
                targetAlpha = gSettings.TargetAlpha,
                trackThicknessPercent = gSettings.TrackThicknessPercent,
                powerVariancePercent = gSettings.PowerVariancePercent,
                cupCaptureSpeedMultiplier = gSettings.CupCaptureSpeedMultiplier,
                strokeNumber = record.StrokeNumber,
                maxStrokesPerPlayer = maxStrokes,
                strokePenaltyPoints = gSettings.StrokePenaltyPoints,
                maxScore = gSettings.MaxScore,
                startAxisPosition = record.StartAxisPosition,
            }, _jsonOptions);

            await QueueWsMessageAsync(WsTopics.Golf, wsPayload, $"golf shot for {record.DisplayName}");

            // Notify other games of drop activity
            await DropGameBroadcastCoordinator.BroadcastOtherDropActiveAsync(webSocketMessenger, logger, WsTopics.Golf, durationSeconds: 6.0);

            return dropToken;
        }

        public async Task ProcessCompletionAsync(PendingDropRecord record)
        {
            var settings = await settingsService.GetSettingsAsync();
            var maxStrokes = Math.Clamp(settings.Golf.MaxStrokesPerPlayer, 1, 5);

            // Multi-stroke: win/lose/bullseye outcome fires once per player round, on their final stroke
            // (sunk, or all strokes used). Earlier strokes are deferred until the round ends.
            if (maxStrokes > 1)
            {
                var userKey = DropGameUserKey.Resolve(record.UserId, record.Username);

                // No round state means the round was already finalized (e.g. session stop cleared it): drop late completions
                if (!_playerRoundStates.TryGetValue(userKey, out var roundState))
                {
                    return;
                }

                lock (roundState)
                {
                    if (roundState.FinalizedDropTokens.Contains(record.DropToken))
                    {
                        return;
                    }

                    if (string.Equals(roundState.LastDropToken, record.DropToken, StringComparison.Ordinal))
                    {
                        roundState.CurrentAxisPos = Math.Clamp(record.LandingX, 0.01, 0.99);
                        roundState.IsSunk = record.IsBullseye;
                        roundState.CurrentScore = record.Score;
                    }
                }

                var isFinalStroke = record.IsBullseye || record.StrokeNumber >= maxStrokes;
                if (!isFinalStroke)
                {
                    var pState = roundState;
                    var stored = false;
                    lock (pState)
                    {
                        if (pState.DeferredOutcomeRecord == null || pState.DeferredOutcomeRecord.StrokeNumber <= record.StrokeNumber)
                        {
                            pState.DeferredOutcomeRecord = record;
                            stored = true;
                        }
                    }

                    // Free play has no session end to flush on: fire after the player has been idle long enough
                    if (stored && _sessionManager.ActiveSession == null)
                    {
                        _ = FireDeferredOutcomeAfterIdleAsync(pState, record);
                    }
                    return;
                }

                lock (roundState)
                {
                    roundState.DeferredOutcomeRecord = null;
                    roundState.FinalizedDropTokens.Add(record.DropToken);
                }
            }

            await FireOutcomeAsync(record);
        }

        /// <summary>Free play only: idle time after a player's unfinished stroke before their round outcome fires.</summary>
        internal TimeSpan FreePlayRoundIdleTimeout { get; set; } = TimeSpan.FromSeconds(45);

        private async Task FireDeferredOutcomeAfterIdleAsync(PlayerGolfRoundState pState, PendingDropRecord record)
        {
            try
            {
                await Task.Delay(FreePlayRoundIdleTimeout);

                var shouldFire = false;
                lock (pState)
                {
                    // Still the latest unfinished stroke (player hasn't shot again or finished) => round is abandoned
                    if (ReferenceEquals(pState.DeferredOutcomeRecord, record) &&
                        string.Equals(pState.LastDropToken, record.DropToken, StringComparison.Ordinal))
                    {
                        pState.DeferredOutcomeRecord = null;
                        pState.FinalizedDropTokens.Add(record.DropToken);
                        // Round is over: next shot starts a brand new hole at the tee
                        pState.StrokesUsed = 0;
                        pState.IsSunk = false;
                        pState.CurrentAxisPos = 0.05;
                        pState.CurrentScore = 0;
                        shouldFire = true;
                    }
                }

                if (shouldFire)
                {
                    await FireOutcomeAsync(record);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fire idle Golf round outcome for {User}", record.DisplayName);
            }
        }

        private async Task FireOutcomeAsync(PendingDropRecord record)
        {
            var settings = await settingsService.GetSettingsAsync();
            var gSettings = settings.Golf;

            var actionVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["User"] = record.Username,
                ["DisplayName"] = record.DisplayName,
                ["UserId"] = record.UserId,
                ["Score"] = record.Score.ToString(CultureInfo.InvariantCulture),
                ["BaseScore"] = record.BaseScore.ToString(CultureInfo.InvariantCulture),
                ["Strokes"] = record.StrokeNumber.ToString(CultureInfo.InvariantCulture),
                ["StrokePenalty"] = record.StrokePenalty.ToString(CultureInfo.InvariantCulture),
                ["IsLipOut"] = record.IsLipOut.ToString().ToLowerInvariant(),
                ["IsWin"] = record.IsWin.ToString().ToLowerInvariant(),
                ["IsBullseye"] = record.IsBullseye.ToString().ToLowerInvariant(),
                ["IsHit"] = record.IsHit.ToString().ToLowerInvariant(),
                ["LandingX"] = record.LandingX.ToString("F4", CultureInfo.InvariantCulture)
            };

            // Trigger DefaultCommand events
            try
            {
                if (record.IsBullseye)
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync("golf", DefaultCommandEventTypes.GolfBullseye, record.EventArgs, actionVars);
                }

                if (record.IsWin)
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync("golf", DefaultCommandEventTypes.GolfWin, record.EventArgs, actionVars);
                }
                else
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync("golf", DefaultCommandEventTypes.GolfLose, record.EventArgs, actionVars);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fire Golf DefaultCommandEvent for {User}", record.DisplayName);
            }

            // Announce in chat
            if (gSettings.AnnounceWinsInChat && (record.IsWin || record.IsBullseye))
            {
                try
                {
                    var bullseyeStr = record.IsBullseye
                        ? (record.StrokeNumber == 1 ? " HOLE-IN-ONE!" : " SUNK IN THE CUP!")
                        : (record.IsLipOut ? " (Lip-Out!)" : "");
                    var msg = (gSettings.WinChatMessageTemplate ?? "{user} finished on the green with {score} pts ({strokes} stroke(s))!{bullseye}")
                        .Replace("{user}", record.DisplayName, StringComparison.OrdinalIgnoreCase)
                        .Replace("{score}", record.Score.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                        .Replace("{strokes}", record.StrokeNumber.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                        .Replace("{bullseye}", bullseyeStr, StringComparison.OrdinalIgnoreCase);

                    await ServiceBackbone.SendChatMessage(msg);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send Golf win chat message for {User}", record.DisplayName);
                }
            }
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;

            var cmdName = command.CommandProperties.CommandName;
            var settings = await settingsService.GetSettingsAsync();
            var gSettings = settings.Golf;

            if (cmdName.Equals("golf", StringComparison.OrdinalIgnoreCase) ||
                cmdName.Equals("putt", StringComparison.OrdinalIgnoreCase) ||
                cmdName.Equals("chip", StringComparison.OrdinalIgnoreCase) ||
                cmdName.Equals("swing", StringComparison.OrdinalIgnoreCase))
            {
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

                var userKey = DropGameUserKey.Resolve(e.UserId, e.Name);
                var maxStrokes = Math.Clamp(gSettings.MaxStrokesPerPlayer, 1, 5);

                if (gSettings.SessionModeEnabled)
                {
                    var session = GetCurrentSession();
                    if (session == null)
                    {
                        if (gSettings.AutoStartOnCommand)
                        {
                            var started = await StartSessionAsync(gSettings.DefaultSessionDurationSeconds, triggeredBy: e.DisplayName);
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

                    // In multi-stroke mode, allow follow-up strokes for a player whose ball is already in play and not yet sunk
                    var isFollowUpStrokeAllowed = false;
                    if (maxStrokes > 1 && _playerRoundStates.TryGetValue(userKey, out var existingState))
                    {
                        lock (existingState)
                        {
                            if (existingState.StrokesUsed > 0 && existingState.StrokesUsed < maxStrokes && !existingState.IsSunk)
                            {
                                isFollowUpStrokeAllowed = true;
                            }
                        }
                    }

                    if (!isFollowUpStrokeAllowed && !_sessionManager.CanUserDrop(userKey, gSettings.MaxDropsPerUserPerSession, out _, out _))
                    {
                        return;
                    }
                }

                await TriggerGolfShotAsync(e, allowDirectUrlOrPath: false);
            }
        }
    }
}

