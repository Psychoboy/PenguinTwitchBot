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

                var theme = DropGameMediaResolver.ResolveTheme(settings, themeOverrideId, cSettings.ActiveThemeId);
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
                var isOtherGameActive = dropRegistry.IsSessionActive(DropGameType.Parachute) ||
                                       dropRegistry.IsSessionActive(DropGameType.Plinko) ||
                                       dropRegistry.IsSessionActive(DropGameType.Balloon);

                var dto = GetCurrentSession()!;
                dto.IsOtherGameActive = isOtherGameActive;
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
                    trackThicknessPercent = cSettings.TrackThicknessPercent
                }, _jsonOptions);

                await QueueWsMessageAsync(WsTopics.Curling, wsPayload, "Curling session start");

                // Cross-game notification
                var otherNotice = DropGameBroadcastCoordinator.CreateOtherGameStatusPayload(isOtherGameActive: true, "curling");
                foreach (var topic in DropGameBroadcastCoordinator.GetOtherGameTopics(WsTopics.Curling))
                {
                    await QueueWsMessageAsync(topic, otherNotice, $"Notify {topic} that Curling is active");
                }

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
                        "curling",
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
                var isOtherGameActive = dropRegistry.IsSessionActive(DropGameType.Parachute) ||
                                       dropRegistry.IsSessionActive(DropGameType.Plinko) ||
                                       dropRegistry.IsSessionActive(DropGameType.Balloon);

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
                    HeaderConfig = headerConfig
                };

                var wsPayload = JsonSerializer.Serialize(new
                {
                    type = "session_end",
                    session = dto,
                    headerConfig,
                    isOtherGameActive
                }, _jsonOptions);

                await QueueWsMessageAsync(WsTopics.Curling, wsPayload, "Curling session_end");

                var otherNotice = DropGameBroadcastCoordinator.CreateOtherGameStatusPayload(isOtherGameActive: false, "curling");
                foreach (var topic in DropGameBroadcastCoordinator.GetOtherGameTopics(WsTopics.Curling))
                {
                    await QueueWsMessageAsync(topic, otherNotice, $"Notify {topic} that Curling is inactive");
                }

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
                        "curling",
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

            // Check Last Call Cutoff if session is active
            var activeSession = _sessionManager.ActiveSession;
            if (activeSession != null)
            {
                var nowUtc = DateTime.UtcNow;
                var remainingSec = (activeSession.EndsAtUtc - nowUtc).TotalSeconds;
                if (remainingSec <= cSettings.LastCallCutoffSeconds)
                {
                    logger.LogInformation("Curling throw for {User} rejected due to Last Call cutoff ({Remaining:F1}s remaining)", eventArgs.DisplayName, remainingSec);
                    return dropToken;
                }
            }

            var condition = conditionOverride ?? (activeSession != null ? _currentSessionCondition : cSettings.IceCondition);
            var targetDistance = targetDistanceOverride ?? (activeSession != null ? _currentSessionTargetDistance : cSettings.TargetDistancePercent);

            // Determine raw power (1-100)
            double rawPower;
            if (power.HasValue)
            {
                rawPower = Math.Clamp(power.Value, 1.0, 100.0);
            }
            else if (cSettings.EnableViewerPowerControl)
            {
                rawPower = ParsePowerFromArgs(eventArgs, cSettings.DefaultPowerMin, cSettings.DefaultPowerMax);
            }
            else
            {
                rawPower = GetRandomPower(cSettings.DefaultPowerMin, cSettings.DefaultPowerMax);
            }

            var theme = DropGameMediaResolver.ResolveTheme(settings, themeOverrideId, cSettings.ActiveThemeId);

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
            }

            // Calculate deterministic physics and rest coordinates
            CurlingSimulationEngine.SimulateCurlingSlide(record, cSettings, rawPower, condition, targetDistance, isReversedOverride: isReversed);

            // Register in server drop registry
            dropRegistry.RegisterDrop(record);

            // Track drop in active session if present
            var userKey = !string.IsNullOrWhiteSpace(eventArgs.UserId) ? eventArgs.UserId : record.Username;
            _sessionManager.RecordUserDrop(userKey);

            // Resolve decal / emote and avatar
            var decalUrl = "";
            var emoteToken = ResolveEmoteTokenFromArgs(eventArgs, emoteOrImage);
            if (!string.IsNullOrWhiteSpace(emoteToken))
            {
                decalUrl = await DropGameMediaResolver.ResolveCharacterOrEmoteUrlAsync(
                    emoteService,
                    eventArgs,
                    emoteToken,
                    fallbackUrl: theme.CurlingStoneImageUrl,
                    allowDirectUrlOrPath: allowDirectUrlOrPath) ?? "";
            }

            if (string.IsNullOrWhiteSpace(decalUrl) && !string.IsNullOrWhiteSpace(theme.CurlingStoneImageUrl))
            {
                decalUrl = theme.CurlingStoneImageUrl;
            }

            var userColor = !string.IsNullOrWhiteSpace(eventArgs.Color) && eventArgs.Color.StartsWith('#')
                ? eventArgs.Color
                : _chatColorService.GetOrAssignColor(userKey, eventArgs.Color);

            var isHorizontal = cSettings.StripPosition is CurlingStripPosition.Bottom or CurlingStripPosition.Top;

            var wsPayload = JsonSerializer.Serialize(new
            {
                type = "curling_slide",
                dropToken,
                username = record.Username,
                displayName = record.DisplayName,
                userColor,
                power = rawPower,
                condition = condition.ToString(),
                targetDistancePercent = targetDistance,
                decalUrl,
                theme = theme,
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
            var notice = DropGameBroadcastCoordinator.CreateOtherDropActivePayload(durationSeconds: 6.0);
            foreach (var topic in DropGameBroadcastCoordinator.GetOtherGameTopics(WsTopics.Curling))
            {
                await QueueWsMessageAsync(topic, notice, $"Curling drop notice to {topic}");
            }

            return dropToken;
        }

        private static int GetRandomPower(int defaultMin, int defaultMax)
        {
            var min = Math.Clamp(defaultMin, 1, 100);
            var max = Math.Clamp(defaultMax, 1, 100);
            if (min > max)
            {
                (min, max) = (max, min);
            }
            return Random.Shared.Next(min, max + 1);
        }

        private static double ParsePowerFromArgs(CommandEventArgs e, int defaultMin, int defaultMax)
        {
            foreach (var arg in e.Args)
            {
                if (int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 1 and <= 100)
                {
                    return parsed;
                }
            }

            var rawArg = (e.Arg ?? "").Trim();
            if (int.TryParse(rawArg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var directParsed) && directParsed is >= 1 and <= 100)
            {
                return directParsed;
            }

            return GetRandomPower(defaultMin, defaultMax);
        }

        private static string? ResolveEmoteTokenFromArgs(CommandEventArgs e, string? explicitEmote)
        {
            if (!string.IsNullOrWhiteSpace(explicitEmote))
            {
                return explicitEmote.Trim();
            }

            foreach (var arg in e.Args)
            {
                var trimmed = arg.Trim();
                if (int.TryParse(trimmed, out _)) continue; // skip power number
                if (!string.IsNullOrWhiteSpace(trimmed)) return trimmed;
            }

            var single = (e.Arg ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(single) && !int.TryParse(single, out _))
            {
                return single;
            }

            return null;
        }

        public async Task ProcessCompletionAsync(PendingDropRecord record)
        {
            var settings = await settingsService.GetSettingsAsync();
            var cSettings = settings.Curling;

            var actionVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["User"] = record.Username,
                ["DisplayName"] = record.DisplayName,
                ["UserId"] = record.UserId,
                ["Score"] = record.Score.ToString(),
                ["IsWin"] = record.IsWin.ToString().ToLowerInvariant(),
                ["IsBullseye"] = record.IsBullseye.ToString().ToLowerInvariant(),
                ["IsHit"] = record.IsHit.ToString().ToLowerInvariant(),
                ["LandingX"] = record.LandingX.ToString("F4")
            };

            // Trigger DefaultCommand events
            try
            {
                if (record.IsBullseye)
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync("curling", DefaultCommandEventTypes.CurlingBullseye, record.EventArgs, actionVars);
                }
                else if (record.IsWin)
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync("curling", DefaultCommandEventTypes.CurlingWin, record.EventArgs, actionVars);
                }
                else
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync("curling", DefaultCommandEventTypes.CurlingLose, record.EventArgs, actionVars);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fire Curling DefaultCommandEvent for {User}", record.DisplayName);
            }

            // Enqueue Win / Bullseye / Lose Action if configured
            var actionToRun = (record.IsBullseye && !string.IsNullOrWhiteSpace(cSettings.BullseyeActionName))
                ? cSettings.BullseyeActionName
                : (record.IsWin ? cSettings.WinActionName : cSettings.LoseActionName);

            if (!string.IsNullOrWhiteSpace(actionToRun))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var actionManagement = scope.ServiceProvider.GetRequiredService<IActionManagementService>();
                    var actionRunner = scope.ServiceProvider.GetRequiredService<IAction>();

                    var allActions = await actionManagement.GetAllActionsAsync();
                    var targetAction = allActions.FirstOrDefault(a =>
                        a.Enabled && a.Name.Equals(actionToRun.Trim(), StringComparison.OrdinalIgnoreCase));

                    if (targetAction != null)
                    {
                        var mergedVars = CommandEventArgsConverter.ToDictionary(record.EventArgs);
                        foreach (var kvp in actionVars)
                        {
                            mergedVars[kvp.Key] = kvp.Value;
                        }
                        await actionRunner.EnqueueAction(mergedVars, targetAction);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to enqueue Curling action {Action} for {User}", actionToRun, record.DisplayName);
                }
            }

            // Announce in chat
            if (cSettings.AnnounceWinsInChat && (record.IsWin || record.IsBullseye))
            {
                try
                {
                    var bullseyeStr = record.IsBullseye ? " BUTTON BULLSEYE!" : "";
                    var msg = (cSettings.WinChatMessageTemplate ?? "{user} landed on the button with {score} pts!{bullseye}")
                        .Replace("{user}", record.DisplayName, StringComparison.OrdinalIgnoreCase)
                        .Replace("{score}", record.Score.ToString(), StringComparison.OrdinalIgnoreCase)
                        .Replace("{bullseye}", bullseyeStr, StringComparison.OrdinalIgnoreCase);

                    await ServiceBackbone.SendChatMessage(msg);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send Curling win chat message for {User}", record.DisplayName);
                }
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
                    await StartSessionAsync(customDuration, triggeredBy: e.DisplayName);
                    return;
                }

                if (e.IsModOrHigher() && firstArg.Equals("stop", StringComparison.OrdinalIgnoreCase))
                {
                    await StopSessionAsync(triggeredBy: e.DisplayName);
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

                    var userKey = !string.IsNullOrWhiteSpace(e.UserId) ? e.UserId : (e.Name ?? "viewer");
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
