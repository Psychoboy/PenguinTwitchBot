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
using PenguinTwitchBot.Bot.Services.Chat;
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
        ILogger<ParachuteDropService> logger,
        IChatColorService? chatColorService = null,
        MediaUploadService? mediaUploadService = null)
        : BaseCommandService(serviceBackbone, commandHandler, "ParachuteDrop", dispatcher), IHostedService, IParachuteDropService
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

        public DropGameSessionDto? GetCurrentSession() => _sessionManager.GetCurrentSessionDto("parachute");

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
                    logger.LogInformation("Starting queued Parachute session ({Duration}s, triggered by {User})",
                        next.DurationSeconds, next.TriggeredBy ?? "Queue");
                    var dto = await StartSessionAsync(next.DurationSeconds, next.ThemeOverrideId, next.TriggeredBy);
                    next.CompletionSource?.TrySetResult(dto);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to start queued Parachute session");
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
            if (!settings.Parachute.GameEnabled)
                throw new InvalidOperationException("Parachute Drop is disabled. Enable it in the game settings first.");
            var duration = durationSeconds.HasValue && durationSeconds.Value > 0
                ? durationSeconds.Value
                : settings.Parachute.DefaultSessionDurationSeconds;

            duration = Math.Clamp(duration, 5, 3600);

            await _sessionManager.SessionLock.WaitAsync();
            try
            {
                if (_sessionManager.HasActiveTimedSession())
                {
                    _sessionManager.EnqueueSession(duration, themeOverrideId, triggeredBy);
                    var queuePosition = _sessionManager.QueuedCount;

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

                var targetCenterX = Math.Round(DropGameMath.RandomDouble(minCenterX, maxCenterX), 4);

                var session = _sessionManager.CreateAndSetActiveSession(duration, themeOverrideId, triggeredBy ?? "System", async state =>
                {
                    try
                    {
                        await StopSessionAsync("timer_expired");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error expiring session for Parachute");
                    }
                });

                session.TargetCenterX = targetCenterX;
                session.TargetHalfWidth = targetHalfWidth;
                session.BullseyeHalfWidth = bullseyeHalfWidth;

                dropRegistry.ClearDropsForGame(DropGameType.Parachute);
                dropRegistry.SetSessionActive(DropGameType.Parachute, true);
                var activeGames = DropGameBroadcastCoordinator.GetActiveGameNames(dropRegistry);
                var isOtherGameActive = activeGames.Any(g => !string.Equals(g, "parachute", StringComparison.OrdinalIgnoreCase));

                var headerConfig = DropGameHeaderConfigDto.FromSettings(settings.Parachute);

                var dto = new DropGameSessionDto
                {
                    GameType = "parachute",
                    IsActive = true,
                    IsQueued = false,
                    QueuePosition = 0,
                    TotalDurationSeconds = duration,
                    RemainingSeconds = duration,
                    StartedAtMs = new DateTimeOffset(session.StartedAtUtc).ToUnixTimeMilliseconds(),
                    EndsAtMs = new DateTimeOffset(session.EndsAtUtc).ToUnixTimeMilliseconds(),
                    TotalDrops = 0,
                    Theme = theme,
                    TargetCenterX = session.TargetCenterX,
                    TargetHalfWidth = session.TargetHalfWidth,
                    BullseyeHalfWidth = session.BullseyeHalfWidth,
                    IsOtherGameActive = isOtherGameActive,
                    ActiveGames = activeGames,
                    HeaderConfig = headerConfig
                };

                var soundBundle = DropGameMediaResolver.ResolveParachuteSoundBundle(settings.Parachute, theme, GetMediaUploadService());
                dto.Sounds = soundBundle;

                var wsPayload = JsonSerializer.Serialize(new { type = "session_start", session = dto, headerConfig, sounds = soundBundle, isOtherGameActive, activeGames }, _jsonOptions);
                await QueueWsMessageAsync(WsTopics.Parachute, wsPayload, "Parachute session_start");

                await DropGameBroadcastCoordinator.BroadcastOtherGameStatusAsync(webSocketMessenger, logger, WsTopics.Parachute, "parachute", isOtherGameActive: true, activeGames: activeGames);

                if (settings.Parachute.AnnounceSessionStatusInChat)
                {
                    var template = settings.Parachute.SessionStartChatMessageTemplate;
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
                    ["Game"] = "ParachuteDrop"
                };

                var startEventArgs = new CommandEventArgs
                {
                    Command = "drop",
                    Name = triggeredBy ?? "System",
                    DisplayName = triggeredBy ?? "System"
                };

                try
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                        "drop",
                        DefaultCommandEventTypes.ParachuteStart,
                        startEventArgs,
                        startVariables);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to fire ParachuteStart trigger");
                }

                logger.LogInformation("Started Parachute session for {Duration}s (Triggered by {User})", duration, triggeredBy ?? "System");
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
                        logger.LogInformation("Removed {Count} queued Parachute session(s) (Triggered by {User})", removedCount, triggeredBy ?? "System");
                        return new DropGameSessionDto { GameType = "parachute", IsActive = false, RemainingSeconds = 0 };
                    }
                    return null;
                }

                dropRegistry.SetSessionActive(DropGameType.Parachute, false);
                var activeGames = DropGameBroadcastCoordinator.GetActiveGameNames(dropRegistry);
                var isOtherGameActive = activeGames.Count > 0;
                var totalDrops = Volatile.Read(ref state.TotalDrops);
                var settings = await settingsService.GetSettingsAsync();
                var theme = DropGameMediaResolver.ResolveTheme(settings, state.ThemeIdOverride, settings.Parachute.ActiveThemeId);

                var headerConfig = DropGameHeaderConfigDto.FromSettings(settings.Parachute);

                dto = new DropGameSessionDto
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
                    BullseyeHalfWidth = state.BullseyeHalfWidth,
                    IsOtherGameActive = isOtherGameActive,
                    ActiveGames = activeGames,
                    HeaderConfig = headerConfig
                };

                var soundBundle = DropGameMediaResolver.ResolveParachuteSoundBundle(settings.Parachute, theme, GetMediaUploadService());
                dto.Sounds = soundBundle;

                var wsPayload = JsonSerializer.Serialize(new { type = "session_end", session = dto, headerConfig, sounds = soundBundle, isOtherGameActive, activeGames }, _jsonOptions);
                await QueueWsMessageAsync(WsTopics.Parachute, wsPayload, "Parachute session_end");

                await DropGameBroadcastCoordinator.BroadcastOtherGameStatusAsync(webSocketMessenger, logger, WsTopics.Parachute, "parachute", isOtherGameActive: false, activeGames: activeGames);

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
                    var endVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["User"] = triggeredBy ?? "System",
                        ["Target"] = triggeredBy ?? "System",
                        ["TotalDrops"] = totalDrops.ToString(CultureInfo.InvariantCulture),
                        ["total_drops"] = totalDrops.ToString(CultureInfo.InvariantCulture),
                        ["Duration"] = state.TotalDurationSeconds.ToString(CultureInfo.InvariantCulture),
                        ["Theme"] = theme.Name,
                        ["TriggeredBy"] = triggeredBy ?? "System",
                        ["Game"] = "ParachuteDrop"
                    };

                    var endEventArgs = new CommandEventArgs
                    {
                        Command = "drop",
                        Name = triggeredBy ?? "System",
                        DisplayName = triggeredBy ?? "System"
                    };

                    try
                    {
                        await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                            "drop",
                            DefaultCommandEventTypes.ParachuteEnd,
                            endEventArgs,
                            endVariables);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to fire ParachuteEnd trigger");
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

        public async Task<string> TriggerParachuteDropAsync(
            CommandEventArgs eventArgs,
            string? emoteOrImage = null,
            string? themeOverrideId = null,
            double? forcedStartX = null,
            double? forcedWindAccelX = null,
            bool skipMinimumDurationCheck = false,
            bool allowDirectUrlOrPath = true,
            double? forcedInitialVx = null,
            double? forcedTargetCenterX = null)
        {
            dropRegistry.CleanupExpiredDrops();
            var settings = await settingsService.GetSettingsAsync();
            var pSettings = settings.Parachute;
            if (!pSettings.GameEnabled && !_sessionManager.HasActiveTimedSession())
                return string.Empty;
            var theme = DropGameMediaResolver.ResolveTheme(settings, themeOverrideId, pSettings.ActiveThemeId);
            var characterImageUrl = await DropGameMediaResolver.ResolveCharacterOrEmoteUrlAsync(emoteService, eventArgs, emoteOrImage, theme.DefaultCharacterImageUrl, allowDirectUrlOrPath);
            dropRegistry.RegisterAllowedProxyUrl(characterImageUrl);
            dropRegistry.RegisterAllowedProxyUrl(theme.ParachuteCanopyImageUrl);
            dropRegistry.RegisterAllowedProxyUrl(theme.DefaultCharacterImageUrl);

            var gravity = Math.Clamp(pSettings.Gravity, 0.1, 10.0);
            var bounciness = Math.Clamp(pSettings.Bounciness, 0.1, 5.0);
            var mass = Math.Clamp(pSettings.Mass, 0.1, 10.0);
            var durationSeconds = Math.Clamp(6.2 / Math.Pow(gravity, 0.25), 3.5, 8.5);
            var totalTicks = Math.Max(30, (int)Math.Round(durationSeconds * ParachuteSimulationEngine.TickRateHz));

            var startX = forcedStartX.HasValue
                ? Math.Clamp(forcedStartX.Value, 0.08, 0.92)
                : DropGameMath.RandomDouble(0.10, 0.90);

            var startY = 0.04;
            var landingY = 0.94;
            var baseVy = (landingY - startY) / durationSeconds;

            const double windAccelX = 0.0;

            // Directional drop angle (no periodic sway): sharper angle = faster lateral speed
            double initialVx;
            double initialTilt;
            if (forcedInitialVx.HasValue)
            {
                initialVx = Math.Clamp(forcedInitialVx.Value, -ParachuteSimulationEngine.MaxVx, ParachuteSimulationEngine.MaxVx);
                initialTilt = Math.Clamp(initialVx * 1.5, -0.60, 0.60);
            }
            else if (forcedStartX.HasValue)
            {
                initialVx = 0.0;
                initialTilt = 0.0;
            }
            else
            {
                // Angle between -55 deg and +55 deg in radians
                var dropAngleRad = DropGameMath.RandomDouble(-0.95, 0.95);
                initialVx = Math.Round(Math.Clamp(baseVy * Math.Tan(dropAngleRad) * 1.15, -ParachuteSimulationEngine.MaxVx, ParachuteSimulationEngine.MaxVx), 4);
                initialTilt = Math.Round(Math.Clamp(dropAngleRad * 0.65, -0.60, 0.60), 4);
            }

            var swayAmplitude = 0.0;
            var swayFrequency = 0.0;
            var swayPhase = 0.0;

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
            var currentSessionState = _sessionManager.ActiveSession;
            if (forcedTargetCenterX.HasValue)
            {
                targetCenterX = Math.Clamp(forcedTargetCenterX.Value, minSafeX, maxSafeX);
            }
            else if (currentSessionState != null && currentSessionState.EndsAtUtc > DateTime.UtcNow && currentSessionState.TargetCenterX.HasValue)
            {
                targetCenterX = Math.Clamp(currentSessionState.TargetCenterX.Value, minSafeX, maxSafeX);
            }
            else
            {
                targetCenterX = Math.Clamp(pSettings.TargetCenterX, minSafeX, maxSafeX);
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
                GameType = DropGameType.Parachute,
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
                StartY = startY,
                LandingY = landingY,
                InitialVx = initialVx,
                InitialVy = baseVy,
                WindAccelX = windAccelX,
                SwayAmplitude = swayAmplitude,
                SwayFrequency = swayFrequency,
                SwayPhase = swayPhase,
                Gravity = gravity,
                Bounciness = bounciness,
                Mass = mass,
                TotalTicks = totalTicks,
                TargetCenterX = targetCenterX,
                TargetHalfWidth = targetHalfWidth,
                CrashLandingScoreMode = pSettings.CrashLandingScoreMode
            };

            ParachuteSimulationEngine.SimulateParachuteTrajectory(
                record,
                0,
                droplets: null,
                rockets: null,
                crashLandingScoreMode: record.CrashLandingScoreMode,
                directHitKillsParachute: pSettings.DirectHitKillsParachute);

            dropRegistry.RegisterDrop(record);

            var activeGames = DropGameBroadcastCoordinator.GetActiveGameNames(dropRegistry);
            var isOtherGameActive = activeGames.Any(g => !string.Equals(g, "parachute", StringComparison.OrdinalIgnoreCase));
            var headerConfig = DropGameHeaderConfigDto.FromSettings(pSettings);

            DropGameSessionDto? activeSessionDto = null;
            var userKey = DropGameUserKey.Resolve(eventArgs.UserId, record.Username);
            if (currentSessionState != null && currentSessionState.EndsAtUtc > now)
            {
                _sessionManager.RecordUserDrop(userKey);
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
                    BullseyeHalfWidth = currentSessionState.BullseyeHalfWidth,
                    IsOtherGameActive = isOtherGameActive,
                    ActiveGames = activeGames,
                    HeaderConfig = headerConfig
                };
            }

            var trajectoryObj = new
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
                gravity,
                bounciness,
                mass,
                nameFontSize = pSettings.NameFontSize,
                targetCenterX,
                targetHalfWidth,
                bullseyeHalfWidth,
                landingX = record.LandingX,
                score = record.Score,
                isHit = record.IsHit,
                isBullseye = record.IsBullseye,
                isWin = record.IsWin,
                totalTicks = record.TotalTicks,
                initialTilt,
                keyframes = record.Keyframes
            };

            var soundBundle = DropGameMediaResolver.ResolveParachuteSoundBundle(pSettings, theme, GetMediaUploadService());
            if (activeSessionDto != null)
            {
                activeSessionDto.Sounds = soundBundle;
            }

            var payload = new
            {
                type = "parachute_drop",
                dropToken,
                username = record.Username,
                displayName = record.DisplayName,
                userColor = record.UserColor,
                color = record.UserColor,
                characterImageUrl = characterImageUrl ?? "",
                isCustomEmote = !string.IsNullOrWhiteSpace(characterImageUrl) && !string.Equals(characterImageUrl, theme.DefaultCharacterImageUrl, StringComparison.OrdinalIgnoreCase),
                theme,
                nameFontSize = pSettings.NameFontSize,
                session = activeSessionDto,
                isOtherGameActive,
                activeGames,
                headerConfig,
                sounds = soundBundle,
                trajectory = trajectoryObj,
                physics = trajectoryObj
            };

            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            await QueueWsMessageAsync(WsTopics.Parachute, json, "parachute drop");

            await DropGameBroadcastCoordinator.BroadcastOtherDropActiveAsync(webSocketMessenger, logger, WsTopics.Parachute, durationSeconds + 6.0);

            return dropToken;
        }

        public async Task<List<DropletInstanceDto>> SpawnDropletsAsync(DropletSpawnRequest request)
        {
            dropRegistry.CleanupExpiredDrops();
            var settings = await settingsService.GetSettingsAsync();
            var pSettings = settings.Parachute;
            if (!pSettings.GameEnabled && !_sessionManager.HasActiveTimedSession()) return [];
            var theme = DropGameMediaResolver.ResolveTheme(settings, null, pSettings.ActiveThemeId);

            var count = Math.Clamp(request.Count > 0 ? request.Count : pSettings.DefaultDropletCount, 1, 30);
            var sizePreset = !string.IsNullOrWhiteSpace(request.SizePreset) ? request.SizePreset : pSettings.DefaultDropletSize;
            var allowedSizes = (request.AllowedRandomSizes != null && request.AllowedRandomSizes.Count > 0)
                ? request.AllowedRandomSizes
                : pSettings.DefaultDropletRandomSizes;
            var speedPreset = !string.IsNullOrWhiteSpace(request.SpeedPreset) ? request.SpeedPreset : pSettings.DefaultDropletSpeed;
            var pattern = !string.IsNullOrWhiteSpace(request.SpawnPattern) ? request.SpawnPattern : pSettings.DefaultDropletPattern;
            var knockbackMultiplier = request.KnockbackMultiplier > 0 ? Math.Clamp(request.KnockbackMultiplier, 0.1, 5.0) : pSettings.DefaultKnockbackMultiplier;

            var fakeEventArgs = new CommandEventArgs
            {
                Arg = request.EmoteOrImage ?? "",
                EmoteUrl = request.NativeEmoteUrl,
                Fragments = request.Fragments != null ? request.Fragments.ToList() : []
            };
            var dropletUrls = await DropGameMediaResolver.ResolveMediaUrlsAsync(
                emoteService, fakeEventArgs, request.EmoteOrImage, theme.DefaultDropletImageUrl, request.AllowDirectUrlOrPath);
            foreach (var url in dropletUrls)
            {
                dropRegistry.RegisterAllowedProxyUrl(url);
            }

            var droplets = new List<DropletInstanceDto>(count);
            var now = DateTime.UtcNow;
            var activeParachutes = dropRegistry.GetActiveDrops(DropGameType.Parachute);

            for (var i = 0; i < count; i++)
            {
                var (sizeLabel, sizeScale, radius, mass) = DropletPatternGenerator.ResolveDropletSize(sizePreset, allowedSizes);
                var (speedLabel, speedScale) = DropletPatternGenerator.ResolveDropletSpeed(speedPreset);
                var spawnTickOffset = i * 4;

                var targetDrop = activeParachutes.Count > 0
                    ? activeParachutes[i % activeParachutes.Count]
                    : null;

                var (startX, startY, vx, vy, gravity) = DropletPatternGenerator.CalculateTrajectory(
                    pattern, i, speedScale, targetDrop, now, spawnTickOffset);

                var dropletImageUrl = dropletUrls.Count > 0
                    ? dropletUrls[RandomNumberGenerator.GetInt32(dropletUrls.Count)]
                    : "";

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
                    ImageUrl = dropletImageUrl,
                    TargetDropToken = pattern.Equals("Targeted", StringComparison.OrdinalIgnoreCase) ? targetDrop?.DropToken : null
                });
            }

            var payload = new
            {
                type = "parachute_droplets",
                triggeredBy = request.TriggeredByUser ?? "Stream",
                theme,
                droplets
            };

            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            await QueueWsMessageAsync(WsTopics.Parachute, json, "parachute droplets");

            await DropGameBroadcastCoordinator.BroadcastOtherDropActiveAsync(webSocketMessenger, logger, WsTopics.Parachute, durationSeconds: 8.0);

            return droplets;
        }

        public async Task<List<RocketInstanceDto>> FireRocketsAsync(RocketFireRequest request)
        {
            dropRegistry.CleanupExpiredDrops();
            var settings = await settingsService.GetSettingsAsync();
            var pSettings = settings.Parachute;
            if (!pSettings.GameEnabled && !_sessionManager.HasActiveTimedSession()) return [];
            if (!pSettings.EnableRockets)
            {
                return [];
            }

            var count = Math.Clamp(request.Count > 0 ? request.Count : 1, 1, 10);
            var pattern = !string.IsNullOrWhiteSpace(request.Pattern) ? request.Pattern : pSettings.RocketLaunchPattern;
            var canopyKillChance = request.CanopyKillChance.HasValue
                ? Math.Clamp(request.CanopyKillChance.Value > 1.0 ? request.CanopyKillChance.Value / 100.0 : request.CanopyKillChance.Value, 0.0, 1.0)
                : pSettings.CanopyKillChance;
            var knockbackMultiplier = request.KnockbackMultiplier.HasValue && request.KnockbackMultiplier.Value > 0
                ? Math.Clamp(request.KnockbackMultiplier.Value, 0.1, 5.0)
                : pSettings.RocketKnockbackMultiplier;
            var scoreMode = request.ScoreModeOverride ?? pSettings.CrashLandingScoreMode;

            var rockets = new List<RocketInstanceDto>(count);
            var now = DateTime.UtcNow;

            var activeParachutes = dropRegistry.GetActiveDrops(DropGameType.Parachute);

            Func<PendingDropRecord, bool> isMidAir = d =>
                (int)Math.Round((now - d.CreatedAtUtc).TotalSeconds * ParachuteSimulationEngine.TickRateHz) < d.TotalTicks;

            var midAirDrops = activeParachutes.Where(isMidAir).ToList();

            PendingDropRecord? targetDrop = null;
            if (string.Equals(pattern, "Targeted", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(request.TargetUser))
                {
                    targetDrop = midAirDrops.FirstOrDefault(d =>
                        d.Username.Equals(request.TargetUser.Trim(), StringComparison.OrdinalIgnoreCase) ||
                        d.DisplayName.Equals(request.TargetUser.Trim(), StringComparison.OrdinalIgnoreCase));
                }
            }

            rockets = RocketTrajectoryGenerator.GenerateRockets(
                new RocketFireRequest
                {
                    Count = count,
                    Pattern = pattern,
                    TargetUser = request.TargetUser,
                    CanopyKillChance = canopyKillChance,
                    KnockbackMultiplier = knockbackMultiplier,
                    ScoreModeOverride = scoreMode,
                    TriggeredByUser = request.TriggeredByUser,
                    DirectHitKillsParachute = pSettings.DirectHitKillsParachute
                },
                targetDrop,
                midAirDrops,
                now);

            foreach (var drop in activeParachutes.Where(isMidAir))
            {
                lock (drop)
                {
                    drop.CrashLandingScoreMode = scoreMode;
                    drop.RocketAttacker ??= request.TriggeredByUser ?? "SubAction";
                }
            }

            var payload = new
            {
                type = "parachute_rockets",
                triggeredBy = request.TriggeredByUser ?? "SubAction",
                rockets
            };

            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            await QueueWsMessageAsync(WsTopics.Parachute, json, "parachute rockets");

            await DropGameBroadcastCoordinator.BroadcastOtherDropActiveAsync(webSocketMessenger, logger, WsTopics.Parachute, durationSeconds: 8.0);

            return rockets;
        }

        public async Task ProcessCompletionAsync(PendingDropRecord record)
        {
            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["User"] = record.DisplayName,
                ["Target"] = record.DisplayName,
                ["Score"] = record.Score.ToString(CultureInfo.InvariantCulture),
                ["IsWin"] = record.IsWin ? "true" : "false",
                ["IsBullseye"] = record.IsBullseye ? "true" : "false",
                ["DistanceFromCenter"] = (Math.Abs(record.LandingX - record.TargetCenterX) * 100.0).ToString("F1", CultureInfo.InvariantCulture),
                ["DropletHits"] = record.DropletHitCount.ToString(CultureInfo.InvariantCulture),
                ["RocketHits"] = record.RocketHitCount.ToString(CultureInfo.InvariantCulture),
                ["ParachuteDestroyed"] = record.ParachuteDestroyed ? "true" : "false",
                ["CrashLanded"] = record.ParachuteDestroyed ? "true" : "false",
                ["Theme"] = record.Theme.Name,
                ["Game"] = "ParachuteDrop"
            };

            if (!record.DefaultCommandTriggerExecuted)
            {
                if (record.ParachuteDestroyed)
                {
                    var attacker = !string.IsNullOrWhiteSpace(record.RocketAttacker) ? record.RocketAttacker : "Stream";
                    var killVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Attacker"] = attacker,
                        ["TriggeredBy"] = attacker,
                        ["Victim"] = record.DisplayName,
                        ["Score"] = record.Score.ToString(CultureInfo.InvariantCulture),
                        ["ParachuteDestroyed"] = "true",
                        ["Theme"] = record.Theme?.Name ?? "Default",
                        ["Game"] = "ParachuteDrop"
                    };

                    try
                    {
                        await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                            "drop",
                            DefaultCommandEventTypes.ParachuteRocketKill,
                            record.EventArgs,
                            killVars);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to fire ParachuteRocketKill trigger for {User}", record.Username);
                    }
                }

                if (record.IsBullseye)
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                        "drop",
                        DefaultCommandEventTypes.ParachuteBullseye,
                        record.EventArgs,
                        variables);

                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                        "drop",
                        DefaultCommandEventTypes.ParachuteWin,
                        record.EventArgs,
                        variables);
                }
                else
                {
                    var eventType = record.IsWin
                        ? DefaultCommandEventTypes.ParachuteWin
                        : DefaultCommandEventTypes.ParachuteLose;

                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                        "drop",
                        eventType,
                        record.EventArgs,
                        variables);
                }

                record.DefaultCommandTriggerExecuted = true;
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
                            if (!pSettings.GameEnabled)
                            {
                                logger.LogInformation("Ignored !drop start from {User} because Parachute Drop is disabled", e.DisplayName);
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

                        if (!pSettings.GameEnabled && !_sessionManager.HasActiveTimedSession())
                        {
                            logger.LogInformation("Ignored !drop from {User} because Parachute Drop is disabled", e.DisplayName);
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

                            var userKey = DropGameUserKey.Resolve(e.UserId, e.Name);
                            if (!_sessionManager.CanUserDrop(userKey, pSettings.MaxDropsPerUserPerSession))
                            {
                                return;
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
                            AllowedRandomSizes = pSettings.DefaultDropletRandomSizes,
                            SpeedPreset = pSettings.DefaultDropletSpeed,
                            SpawnPattern = pSettings.DefaultDropletPattern,
                            KnockbackMultiplier = pSettings.DefaultKnockbackMultiplier,
                            EmoteOrImage = e.Arg,
                            NativeEmoteUrl = e.EmoteUrl,
                            Fragments = e.Fragments,
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
                dropRegistry.SetSessionActive(DropGameType.Parachute, false);
                _sessionManager.Dispose();
            }
            catch { }
            return Task.CompletedTask;
        }
    }
}
