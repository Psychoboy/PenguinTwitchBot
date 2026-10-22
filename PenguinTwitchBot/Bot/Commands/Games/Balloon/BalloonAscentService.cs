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
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Services;

namespace PenguinTwitchBot.Bot.Commands.Games.Balloon
{
    public class BalloonAscentService(
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        IDefaultCommandTriggerService defaultCommandTriggerService,
        IServiceScopeFactory scopeFactory,
        IEmoteService emoteService,
        IWebSocketMessenger webSocketMessenger,
        Application.Notifications.IPenguinDispatcher dispatcher,
        IDropGamesSettingsService settingsService,
        IDropRegistry dropRegistry,
        ILogger<BalloonAscentService> logger,
        IChatColorService? chatColorService = null,
        MediaUploadService? mediaUploadService = null)
        : BaseCommandService(serviceBackbone, commandHandler, "BalloonAscent", dispatcher), IHostedService, IBalloonAscentService
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

        public override async Task Register()
        {
            await RegisterDefaultCommand("balloon", this, ModuleName, Rank.Viewer, userCooldown: 10);
            await RegisterDefaultCommand("float", this, ModuleName, Rank.Viewer, userCooldown: 10);
            await RegisterDefaultCommand("ascent", this, ModuleName, Rank.Viewer, userCooldown: 10);
            await RegisterDefaultCommand("balloonhazards", this, ModuleName, Rank.Moderator, userCooldown: 5);
        }

        public Task StartAsync(CancellationToken cancellationToken) => Register();

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                dropRegistry.SetSessionActive(DropGameType.Balloon, false);
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

        public DropGameSessionDto? GetCurrentSession() => _sessionManager.GetCurrentSessionDto("balloon");

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
                var now = DateTime.UtcNow;
                var settings = await settingsService.GetSettingsAsync();
                var bSettings = settings.Balloon;
                if (!bSettings.GameEnabled)
                    throw new InvalidOperationException("Balloon Ascent is disabled. Enable it in the game settings first.");

                if (_sessionManager.HasActiveTimedSession())
                {
                    _sessionManager.EnqueueSession(durationSeconds, themeOverrideId, triggeredBy);
                    var queuedDuration = durationSeconds ?? bSettings.DefaultSessionDurationSeconds;
                    return new DropGameSessionDto
                    {
                        GameType = "balloon",
                        IsActive = false,
                        IsQueued = true,
                        QueuePosition = _sessionManager.QueuedCount,
                        TotalDurationSeconds = queuedDuration,
                        RemainingSeconds = queuedDuration,
                        TotalDrops = 0
                    };
                }

                var duration = Math.Clamp(durationSeconds ?? bSettings.DefaultSessionDurationSeconds, 10, 600);
                var theme = DropGameMediaResolver.ResolveTheme(settings, themeOverrideId, bSettings.ActiveThemeId);

                var targetHalfWidth = Math.Clamp((bSettings.TargetWidthPercent / 100.0) / 2.0, 0.03, 0.40);
                var targetCenterX = Math.Clamp(bSettings.TargetCenterX, targetHalfWidth, 1.0 - targetHalfWidth);

                var session = _sessionManager.CreateAndSetActiveSession(duration, theme.Id, triggeredBy ?? "System", async state =>
                {
                    try
                    {
                        await StopSessionAsync("Timer");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error handling Balloon session expiration");
                    }
                });
                session.TargetCenterX = targetCenterX;
                session.TargetHalfWidth = targetHalfWidth;
                session.BullseyeHalfWidth = targetHalfWidth * 0.25;

                var headerConfig = DropGameHeaderConfigDto.FromSettings(bSettings);

                dropRegistry.SetSessionActive(DropGameType.Balloon, true);
                var activeGames = DropGameBroadcastCoordinator.GetActiveGameNames(dropRegistry);
                var isOtherGameActive = activeGames.Any(g => !string.Equals(g, "balloon", StringComparison.OrdinalIgnoreCase));

                var dto = GetCurrentSession()!;
                dto.IsOtherGameActive = isOtherGameActive;
                dto.ActiveGames = activeGames;
                dto.HeaderConfig = headerConfig;
                var wsPayload = JsonSerializer.Serialize(new
                {
                    type = "session_start",
                    session = dto,
                    theme = theme,
                    headerConfig = headerConfig,
                    isOtherGameActive = isOtherGameActive,
                    activeGames = activeGames,
                    target = new
                    {
                        centerX = targetCenterX,
                        halfWidth = targetHalfWidth,
                        speed = bSettings.TargetSpeed,
                        alpha = bSettings.TargetAlpha
                    }
                }, _jsonOptions);

                await QueueWsMessageAsync(WsTopics.Balloon, wsPayload, "Balloon session_start");

                await DropGameBroadcastCoordinator.BroadcastOtherGameStatusAsync(webSocketMessenger, logger, WsTopics.Balloon, "balloon", isOtherGameActive: true, activeGames: activeGames);

                if (!string.Equals(triggeredBy, "restart", StringComparison.OrdinalIgnoreCase))
                {
                    if (bSettings.AnnounceSessionStatusInChat)
                    {
                        var template = bSettings.SessionStartChatMessageTemplate;
                        if (!string.IsNullOrWhiteSpace(template))
                        {
                            var msg = template.Replace("{duration}", duration.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
                            await ServiceBackbone.SendChatMessage(msg);
                        }
                    }
                }

                var startVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["User"] = triggeredBy ?? "System",
                    ["Target"] = triggeredBy ?? "System",
                    ["Duration"] = duration.ToString(CultureInfo.InvariantCulture),
                    ["Theme"] = theme.Name,
                    ["TriggeredBy"] = triggeredBy ?? "System",
                    ["Game"] = "BalloonAscent"
                };

                var startEventArgs = new CommandEventArgs
                {
                    Command = "balloon",
                    Name = triggeredBy ?? "System",
                    DisplayName = triggeredBy ?? "System"
                };

                try
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                        "balloon",
                        DefaultCommandEventTypes.BalloonStart,
                        startEventArgs,
                        startVariables);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to fire BalloonStart trigger");
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

                dropRegistry.SetSessionActive(DropGameType.Balloon, false);
                var activeGames = DropGameBroadcastCoordinator.GetActiveGameNames(dropRegistry);
                var isOtherGameActive = activeGames.Count > 0;

                var settings = await settingsService.GetSettingsAsync();
                var bSettings = settings.Balloon;
                var headerConfig = DropGameHeaderConfigDto.FromSettings(bSettings);

                dto = new DropGameSessionDto
                {
                    GameType = "balloon",
                    IsActive = false,
                    TotalDurationSeconds = session.TotalDurationSeconds,
                    RemainingSeconds = 0,
                    StartedAtMs = new DateTimeOffset(session.StartedAtUtc).ToUnixTimeMilliseconds(),
                    EndsAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    TotalDrops = Volatile.Read(ref session.TotalDrops),
                    TargetCenterX = session.TargetCenterX,
                    TargetHalfWidth = session.TargetHalfWidth,
                    BullseyeHalfWidth = session.BullseyeHalfWidth,
                    IsOtherGameActive = isOtherGameActive,
                    ActiveGames = activeGames,
                    HeaderConfig = headerConfig
                };

                var wsPayload = JsonSerializer.Serialize(new
                {
                    type = "session_end",
                    session = dto,
                    headerConfig = headerConfig,
                    isOtherGameActive = isOtherGameActive,
                    activeGames = activeGames
                }, _jsonOptions);

                await QueueWsMessageAsync(WsTopics.Balloon, wsPayload, "Balloon session_end");

                await DropGameBroadcastCoordinator.BroadcastOtherGameStatusAsync(webSocketMessenger, logger, WsTopics.Balloon, "balloon", isOtherGameActive: false, activeGames: activeGames);

                if (!string.Equals(triggeredBy, "restart", StringComparison.OrdinalIgnoreCase))
                {
                    if (bSettings.AnnounceSessionStatusInChat)
                    {
                        var template = bSettings.SessionEndChatMessageTemplate;
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
                    ["Target"] = triggeredBy ?? "System",
                    ["TotalDrops"] = dto.TotalDrops.ToString(CultureInfo.InvariantCulture),
                    ["total_drops"] = dto.TotalDrops.ToString(CultureInfo.InvariantCulture),
                    ["Duration"] = dto.TotalDurationSeconds.ToString(CultureInfo.InvariantCulture),
                    ["TriggeredBy"] = triggeredBy ?? "System",
                    ["Game"] = "BalloonAscent"
                };

                var endEventArgs = new CommandEventArgs
                {
                    Command = "balloon",
                    Name = triggeredBy ?? "System",
                    DisplayName = triggeredBy ?? "System"
                };

                try
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                        "balloon",
                        DefaultCommandEventTypes.BalloonEnd,
                        endEventArgs,
                        endVariables);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to fire BalloonEnd trigger");
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
                    var dto = await StartSessionAsync(next.DurationSeconds, next.ThemeOverrideId, next.TriggeredBy);
                    next.CompletionSource?.TrySetResult(dto);
                }
                catch (Exception ex)
                {
                    next.CompletionSource?.TrySetException(ex);
                }
            }
        }

        public async Task<string> TriggerBalloonAscentAsync(
            CommandEventArgs eventArgs,
            string? emoteOrImage = null,
            string? themeOverrideId = null,
            double? forcedStartX = null,
            bool skipMinimumDurationCheck = false,
            bool allowDirectUrlOrPath = true,
            double? forcedInitialVx = null,
            double? forcedTargetCenterX = null)
        {
            dropRegistry.CleanupExpiredDrops();
            var settings = await settingsService.GetSettingsAsync();
            var bSettings = settings.Balloon;
            if (!bSettings.GameEnabled && !_sessionManager.HasActiveTimedSession())
                return string.Empty;
            var theme = DropGameMediaResolver.ResolveTheme(settings, themeOverrideId, bSettings.ActiveThemeId);
            var characterImageUrl = await DropGameMediaResolver.ResolveCharacterOrEmoteUrlAsync(emoteService, eventArgs, emoteOrImage, theme.DefaultCharacterImageUrl, allowDirectUrlOrPath);

            var buoyancy = Math.Clamp(bSettings.Buoyancy, 0.1, 10.0);
            var durationSeconds = Math.Clamp(5.8 / Math.Pow(buoyancy, 0.25), 3.2, 8.5);
            var totalTicks = Math.Max(30, (int)Math.Round(durationSeconds * BalloonAscentSimulationEngine.TickRateHz));

            var startX = forcedStartX.HasValue
                ? Math.Clamp(forcedStartX.Value, 0.08, 0.92)
                : DropGameMath.RandomDouble(0.10, 0.90);

            var startY = BalloonAscentSimulationEngine.LaunchY; // 0.94
            var landingY = BalloonAscentSimulationEngine.LandingY; // 0.10
            var baseVy = (landingY - startY) / durationSeconds; // negative velocity upward

            double initialVx = forcedInitialVx.HasValue
                ? Math.Clamp(forcedInitialVx.Value, -BalloonAscentSimulationEngine.MaxVx, BalloonAscentSimulationEngine.MaxVx)
                : DropGameMath.RandomDouble(-0.06, 0.06);

            var targetHalfWidth = Math.Clamp((bSettings.TargetWidthPercent / 100.0) / 2.0, 0.03, 0.40);
            var targetCenterX = forcedTargetCenterX.HasValue
                ? Math.Clamp(forcedTargetCenterX.Value, targetHalfWidth, 1.0 - targetHalfWidth)
                : Math.Clamp(bSettings.TargetCenterX, targetHalfWidth, 1.0 - targetHalfWidth);

            var dropToken = Guid.NewGuid().ToString("N");
            var now = DateTime.UtcNow;

            var userColor = !string.IsNullOrWhiteSpace(eventArgs.Color) && eventArgs.Color.StartsWith('#')
                ? eventArgs.Color
                : _chatColorService.GetOrAssignColor(
                    !string.IsNullOrWhiteSpace(eventArgs.UserId) ? eventArgs.UserId : (string.IsNullOrWhiteSpace(eventArgs.Name) ? "viewer" : eventArgs.Name),
                    eventArgs.Color);

            var record = new PendingDropRecord
            {
                DropToken = dropToken,
                GameType = DropGameType.Balloon,
                Username = eventArgs.Name,
                DisplayName = eventArgs.DisplayName,
                UserId = eventArgs.UserId,
                UserColor = userColor,
                EventArgs = eventArgs,
                Theme = theme,
                CreatedAtUtc = now,
                EarliestCompletionUtc = now.AddSeconds(durationSeconds * 0.5),
                ExpiresAtUtc = now.AddSeconds(durationSeconds + 15.0),
                StartX = Math.Round(startX, 4),
                StartY = Math.Round(startY, 4),
                LandingY = Math.Round(landingY, 4),
                InitialVx = Math.Round(initialVx, 4),
                InitialVy = Math.Round(baseVy, 4),
                TotalTicks = totalTicks,
                TargetCenterX = Math.Round(targetCenterX, 4),
                TargetHalfWidth = Math.Round(targetHalfWidth, 4),
                Bounciness = bSettings.Bounciness,
                Mass = bSettings.Mass
            };

            BalloonAscentSimulationEngine.SimulateBalloonTrajectory(record, bSettings.PoppedScoreMode);
            dropRegistry.RegisterDrop(record);

            var userKey = DropGameUserKey.Resolve(eventArgs.UserId, record.Username);
            _sessionManager.RecordUserDrop(userKey);

            var soundsDto = DropGameMediaResolver.ResolveBalloonSoundBundle(bSettings, theme, GetMediaUploadService());

            var activeGames = DropGameBroadcastCoordinator.GetActiveGameNames(dropRegistry);
            var isOtherGameActive = activeGames.Any(g => !string.Equals(g, "balloon", StringComparison.OrdinalIgnoreCase));

            var wsMessage = new
            {
                type = "balloon_drop",
                dropToken = record.DropToken,
                user = record.DisplayName,
                userColor = record.UserColor,
                nameFontSize = bSettings.NameFontSize,
                characterImageUrl = characterImageUrl,
                balloonCanopyImageUrl = theme.BalloonCanopyImageUrl,
                cloudPlatformImageUrl = theme.BalloonCloudPlatformImageUrl,
                startX = record.StartX,
                startY = record.StartY,
                landingY = record.LandingY,
                initialVx = record.InitialVx,
                initialVy = record.InitialVy,
                durationSeconds = durationSeconds,
                totalTicks = record.TotalTicks,
                isOtherGameActive,
                activeGames,
                target = new
                {
                    centerX = record.TargetCenterX,
                    halfWidth = record.TargetHalfWidth,
                    speed = bSettings.TargetSpeed,
                    alpha = bSettings.TargetAlpha
                },
                headerConfig = DropGameHeaderConfigDto.FromSettings(bSettings),
                physics = new
                {
                    buoyancy = bSettings.Buoyancy,
                    bounciness = bSettings.Bounciness,
                    mass = bSettings.Mass,
                    swayAmount = bSettings.SwayAmount
                },
                theme = theme,
                sounds = soundsDto
            };

            var json = JsonSerializer.Serialize(wsMessage, _jsonOptions);
            await QueueWsMessageAsync(WsTopics.Balloon, json, "balloon drop");

            // Also notify other minigame overlays to prevent visual collisions
            await DropGameBroadcastCoordinator.BroadcastOtherDropActiveAsync(webSocketMessenger, logger, WsTopics.Balloon, durationSeconds + 6.0);

            return dropToken;
        }

        public async Task<List<DropletInstanceDto>> SpawnHazardsAsync(BalloonHazardSpawnRequest request)
        {
            var settings = await settingsService.GetSettingsAsync();
            var bSettings = settings.Balloon;
            if (!bSettings.GameEnabled && !_sessionManager.HasActiveTimedSession())
                return [];
            if (!bSettings.EnableHazards)
            {
                return new List<DropletInstanceDto>();
            }

            var requestedType = (request.HazardType ?? "Random").Trim();
            var isRandom = requestedType.Equals("Random", StringComparison.OrdinalIgnoreCase);

            if (!isRandom)
            {
                if (requestedType.Equals("DartPop", StringComparison.OrdinalIgnoreCase) && !bSettings.EnableDarts)
                    return new List<DropletInstanceDto>();
                if (requestedType.Equals("HeavyPush", StringComparison.OrdinalIgnoreCase) && !bSettings.EnableAnvils)
                    return new List<DropletInstanceDto>();
                if (requestedType.Equals("Nudge", StringComparison.OrdinalIgnoreCase) && !bSettings.EnableNudges)
                    return new List<DropletInstanceDto>();
                if (requestedType.Equals("WindGust", StringComparison.OrdinalIgnoreCase) && !bSettings.EnableWindGusts)
                    return new List<DropletInstanceDto>();
            }

            var now = DateTime.UtcNow;

            var count = Math.Clamp(request.Count, 1, 25);
            var (speedLabel, speedScale) = BalloonHazardPatternGenerator.ResolveHazardSpeed(request.SpeedPreset);

            var activeDrops = dropRegistry.GetActiveDrops(DropGameType.Balloon);
            PendingDropRecord? targetDrop = null;
            if (!string.IsNullOrWhiteSpace(request.TargetDropToken))
            {
                targetDrop = activeDrops.FirstOrDefault(d => d.DropToken == request.TargetDropToken);
            }
            else if (!string.IsNullOrWhiteSpace(request.TargetUser))
            {
                var targetName = request.TargetUser.Trim();
                targetDrop = activeDrops.FirstOrDefault(d =>
                    d.Username.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
                    d.DisplayName.Equals(targetName, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                targetDrop = activeDrops.OrderBy(_ => RandomNumberGenerator.GetInt32(0, 1000)).FirstOrDefault();
            }

            var theme = DropGameMediaResolver.ResolveTheme(settings, null, bSettings.ActiveThemeId);
            var enabledTypes = new List<string>();
            if (bSettings.EnableDarts) enabledTypes.Add("DartPop");
            if (bSettings.EnableAnvils) enabledTypes.Add("HeavyPush");
            if (bSettings.EnableNudges) enabledTypes.Add("Nudge");
            if (bSettings.EnableWindGusts) enabledTypes.Add("WindGust");
            if (enabledTypes.Count == 0)
            {
                return new List<DropletInstanceDto>();
            }

            async Task<string> ResolveHazardImageAsync(string? typeEmote, string? themeUrl)
            {
                var candidate = !string.IsNullOrWhiteSpace(request.EmoteOrImage)
                    ? request.EmoteOrImage
                    : (!string.IsNullOrWhiteSpace(typeEmote)
                        ? typeEmote
                        : (!string.IsNullOrWhiteSpace(themeUrl)
                            ? themeUrl
                            : (!string.IsNullOrWhiteSpace(bSettings.DefaultHazardEmote)
                                ? bSettings.DefaultHazardEmote
                                : theme.BalloonDebrisImageUrl)));

                if (string.IsNullOrWhiteSpace(candidate)) return "";
                var res = await DropGameMediaResolver.ResolveMediaUrlsAsync(emoteService, null, candidate, null, true);
                return res.Count > 0 ? res[0] : "";
            }

            var typeImages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DartPop"] = await ResolveHazardImageAsync(bSettings.DefaultDartEmote, theme.BalloonDartImageUrl),
                ["HeavyPush"] = await ResolveHazardImageAsync(bSettings.DefaultAnvilEmote, theme.BalloonAnvilImageUrl),
                ["WindGust"] = await ResolveHazardImageAsync(bSettings.DefaultWindGustEmote, theme.BalloonWindGustImageUrl),
                ["Nudge"] = await ResolveHazardImageAsync(bSettings.DefaultNudgeEmote, theme.BalloonNudgeImageUrl)
            };

            var instances = new List<DropletInstanceDto>();
            for (var i = 0; i < count; i++)
            {
                var actualType = isRandom
                    ? enabledTypes[RandomNumberGenerator.GetInt32(enabledTypes.Count)]
                    : requestedType;

                var (sizeLabel, sizeScale, radius, mass) = BalloonHazardPatternGenerator.ResolveHazardPhysics(actualType, request.SizePreset);
                var hazardImg = typeImages.TryGetValue(actualType, out var imgUrl) ? imgUrl : "";

                var offset = i * 4;
                var (startX, startY, vx, vy, gravity) = BalloonHazardPatternGenerator.CalculateTrajectory(
                    request.Pattern,
                    actualType,
                    i,
                    speedScale,
                    targetDrop,
                    now,
                    offset);

                instances.Add(new DropletInstanceDto
                {
                    Id = Guid.NewGuid().ToString("N"),
                    SpawnTickOffset = offset,
                    StartX = Math.Round(startX, 4),
                    StartY = Math.Round(startY, 4),
                    Vx = Math.Round(vx, 4),
                    Vy = Math.Round(vy, 4),
                    Gravity = Math.Round(gravity, 4),
                    Radius = Math.Round(radius, 4),
                    Mass = Math.Round(mass, 4),
                    SizeScale = Math.Round(sizeScale, 4),
                    SpeedScale = Math.Round(speedScale, 4),
                    SizeLabel = sizeLabel,
                    SpeedLabel = speedLabel,
                    ImageUrl = hazardImg,
                    TargetDropToken = targetDrop?.DropToken,
                    HazardType = actualType
                });
            }

            var wsPayload = JsonSerializer.Serialize(new
            {
                type = "balloon_hazards",
                hazards = instances
            }, _jsonOptions);

            await QueueWsMessageAsync(WsTopics.Balloon, wsPayload, "balloon hazards");

            await DropGameBroadcastCoordinator.BroadcastOtherDropActiveAsync(webSocketMessenger, logger, WsTopics.Balloon, durationSeconds: 8.0);

            return instances;
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
                ["BalloonPopped"] = record.BalloonPopped.ToString().ToLowerInvariant(),
                ["DistanceFromCenter"] = (Math.Abs(record.LandingX - record.TargetCenterX) * 100.0).ToString("F1", CultureInfo.InvariantCulture),
                ["LandingX"] = record.LandingX.ToString("F4")
            };

            // Trigger DefaultCommand events
            try
            {
                if (record.IsBullseye)
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync("balloon", DefaultCommandEventTypes.BalloonBullseye, record.EventArgs, actionVars);
                }

                if (record.IsWin || record.IsBullseye)
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync("balloon", DefaultCommandEventTypes.BalloonWin, record.EventArgs, actionVars);
                }
                else if (record.BalloonPopped)
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync("balloon", DefaultCommandEventTypes.BalloonPopped, record.EventArgs, actionVars);
                }
                else
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync("balloon", DefaultCommandEventTypes.BalloonLose, record.EventArgs, actionVars);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fire Balloon DefaultCommandEvent for {User}", record.DisplayName);
            }

        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;

            var cmdName = command.CommandProperties.CommandName;
            var settings = await settingsService.GetSettingsAsync();
            var bSettings = settings.Balloon;

            if (cmdName.Equals("balloon", StringComparison.OrdinalIgnoreCase) ||
                cmdName.Equals("float", StringComparison.OrdinalIgnoreCase) ||
                cmdName.Equals("ascent", StringComparison.OrdinalIgnoreCase))
            {
                var firstArg = e.Args.Count > 0 ? e.Args[0] : (e.Arg ?? "").Trim();

                if (e.IsModOrHigher() && firstArg.Equals("start", StringComparison.OrdinalIgnoreCase))
                {
                    int? customDuration = null;
                    if (e.Args.Count > 1 && int.TryParse(e.Args[1], out var parsedDuration))
                    {
                        customDuration = parsedDuration;
                    }
                    if (!bSettings.GameEnabled)
                    {
                        logger.LogInformation("Ignored !balloon start from {User} because Balloon Ascent is disabled", e.DisplayName);
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

                if (!bSettings.GameEnabled && !_sessionManager.HasActiveTimedSession())
                {
                    logger.LogInformation("Ignored !balloon from {User} because Balloon Ascent is disabled", e.DisplayName);
                    return;
                }

                if (bSettings.SessionModeEnabled)
                {
                    var session = GetCurrentSession();
                    if (session == null)
                    {
                        if (bSettings.AutoStartOnCommand)
                        {
                            var started = await StartSessionAsync(bSettings.DefaultSessionDurationSeconds, triggeredBy: e.DisplayName);
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
                    if (!_sessionManager.CanUserDrop(userKey, bSettings.MaxDropsPerUserPerSession, out _, out _))
                    {
                        return;
                    }
                }

                var emote = e.Arg?.Trim();
                await TriggerBalloonAscentAsync(e, emote, allowDirectUrlOrPath: false);
            }
            else if (cmdName.Equals("balloonhazards", StringComparison.OrdinalIgnoreCase))
            {
                var req = new BalloonHazardSpawnRequest
                {
                    HazardType = "Random",
                    Count = 6,
                    EmoteOrImage = e.Arg?.Trim(),
                    TriggeredBy = e.DisplayName
                };
                await SpawnHazardsAsync(req);
            }
        }
    }
}

