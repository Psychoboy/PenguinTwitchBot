using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Bot.Services.Chat;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Services;

namespace PenguinTwitchBot.Bot.Commands.Games.Racing
{
    public class RacingGameService(
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        IDefaultCommandTriggerService defaultCommandTriggerService,
        IServiceScopeFactory scopeFactory,
        IEmoteService emoteService,
        IWebSocketMessenger webSocketMessenger,
        Application.Notifications.IPenguinDispatcher dispatcher,
        IDropGamesSettingsService settingsService,
        IDropRegistry dropRegistry,
        ILogger<RacingGameService> logger,
        IChatColorService? chatColorService = null,
        MediaUploadService? mediaUploadService = null,
        IPointsSystem? pointsSystem = null)
        : BaseCommandService(serviceBackbone, commandHandler, "Racing", dispatcher), IHostedService, IRacingGameService, IDisposable
    {
        private readonly IChatColorService _chatColorService = chatColorService ?? new ChatColorService();
        private readonly MediaUploadService? _mediaUploadService = mediaUploadService;
        private readonly IPointsSystem? _pointsSystem = pointsSystem;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        private readonly DropGameSessionManager _sessionManager = new(logger);
        private readonly List<PendingDropRecord> _lobbyRacers = [];
        private readonly object _racerLock = new();

        private RacingGameMode _currentSessionGameMode = RacingGameMode.GrandPrix;
        private RacingTrackType _currentSessionTrackType = RacingTrackType.Perimeter;
        private int _currentSessionLaps = 2;
        private bool _isRaceRunning = false;
        private RacingTournamentState? _activeTournament;
        private readonly HashSet<string> _singleRaceRemainingTokens = [];
        private CancellationTokenSource? _heatWatchdogCts;

        public class RacingTournamentState
        {
            public List<List<PendingDropRecord>> Heats { get; set; } = [];
            public int CurrentHeatIndex { get; set; } = 0;
            public int AdvancePerHeat { get; set; } = 2;
            public List<PendingDropRecord> QualifiedFinalists { get; set; } = [];
            public HashSet<string> CurrentHeatFinishedTokens { get; set; } = [];
            public bool IsChampionshipFinal { get; set; } = false;
            public HashSet<string> FinalFinishedTokens { get; set; } = [];
            public int TotalHeats => Heats.Count;
        }

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
            await RegisterDefaultCommand("race", this, ModuleName, Rank.Viewer, userCooldown: 5);
            await RegisterDefaultCommand("racing", this, ModuleName, Rank.Viewer, userCooldown: 5);
            await RegisterDefaultCommand("kart", this, ModuleName, Rank.Viewer, userCooldown: 5);
            await RegisterDefaultCommand("grandprix", this, ModuleName, Rank.Viewer, userCooldown: 5);
            await RegisterDefaultCommand("timetrial", this, ModuleName, Rank.Viewer, userCooldown: 5);
            await RegisterDefaultCommand("tt", this, ModuleName, Rank.Viewer, userCooldown: 5);
        }

        public Task StartAsync(CancellationToken cancellationToken) => Register();

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                dropRegistry.SetSessionActive(DropGameType.Racing, false);
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

        public DropGameSessionDto? GetCurrentSession() => _sessionManager.GetCurrentSessionDto("racing");

        public int GetQueuedSessionCount() => _sessionManager.QueuedCount;

        public Task ClearSessionQueueAsync()
        {
            _sessionManager.RemoveQueuedSessions();
            return Task.CompletedTask;
        }

        public async Task<DropGameSessionDto> StartRaceLobbyAsync(
            int? lobbyDurationSeconds = null,
            string? themeOverrideId = null,
            string? triggeredBy = null,
            RacingGameMode? gameModeOverride = null,
            RacingTrackType? trackTypeOverride = null,
            int? lapsOverride = null)
        {
            await _sessionManager.SessionLock.WaitAsync();
            try
            {
                var settings = await settingsService.GetSettingsAsync();
                var rSettings = settings.Racing;
                if (!rSettings.GameEnabled)
                    throw new InvalidOperationException("Racing Minigame is disabled. Enable it in the game settings first.");

                var duration = Math.Clamp(lobbyDurationSeconds ?? rSettings.LobbyDurationSeconds, 15, 180);

                lock (_racerLock)
                {
                    _lobbyRacers.Clear();
                    _isRaceRunning = false;
                    _currentSessionGameMode = gameModeOverride ?? rSettings.DefaultGameMode;
                    _currentSessionTrackType = trackTypeOverride ?? rSettings.TrackType;
                    _currentSessionLaps = lapsOverride ?? rSettings.TotalLaps;
                }

                var theme = await DropGameMediaResolver.ResolveAndPrepareThemeAsync(settings, themeOverrideId, rSettings.ActiveThemeId, emoteService, dropRegistry);
                var soundBundle = DropGameMediaResolver.ResolveRacingSoundBundle(rSettings, theme, GetMediaUploadService());
                var headerConfig = DropGameHeaderConfigDto.FromSettings(rSettings);

                var session = _sessionManager.CreateAndSetActiveSession(duration, theme.Id, triggeredBy ?? "System", async _ =>
                {
                    try
                    {
                        await OnLobbyTimerExpiredAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error handling Racing lobby countdown expiration");
                    }
                });

                dropRegistry.SetSessionActive(DropGameType.Racing, true);
                var activeGames = DropGameBroadcastCoordinator.GetActiveGameNames(dropRegistry);
                var isOtherGameActive = activeGames.Any(g => !string.Equals(g, "racing", StringComparison.OrdinalIgnoreCase));

                var dto = GetCurrentSession()!;
                dto.IsOtherGameActive = isOtherGameActive;
                dto.ActiveGames = activeGames;
                dto.HeaderConfig = headerConfig;
                dto.Sounds = soundBundle;

                var wsPayload = JsonSerializer.Serialize(new
                {
                    type = "race_lobby_start",
                    session = dto,
                    theme,
                    headerConfig,
                    isOtherGameActive,
                    activeGames,
                    sounds = soundBundle,
                    gameMode = _currentSessionGameMode.ToString(),
                    trackType = _currentSessionTrackType.ToString(),
                    stripPosition = rSettings.StripPosition.ToString(),
                    totalLaps = _currentSessionLaps,
                    lobbyDurationSeconds = duration,
                    maxRacers = rSettings.MaxRacersPerHeat,
                    trackAlpha = rSettings.TrackAlpha,
                    showTrackBorders = rSettings.ShowTrackBorders,
                    trackThicknessPercent = rSettings.TrackThicknessPercent,
                    messages = rSettings.Messages
                }, _jsonOptions);

                await QueueWsMessageAsync(WsTopics.Racing, wsPayload, "Racing race_lobby_start");
                await DropGameBroadcastCoordinator.BroadcastOtherGameStatusAsync(webSocketMessenger, logger, WsTopics.Racing, "racing", isOtherGameActive: true, activeGames: activeGames);

                if (rSettings.AnnounceSessionStatusInChat)
                {
                    var msg = RacingMessageSettings.Format(
                        rSettings.Messages.SessionStart,
                        RacingMessageSettings.DefaultSessionStart,
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["duration"] = duration.ToString(CultureInfo.InvariantCulture),
                            ["laps"] = _currentSessionLaps.ToString(CultureInfo.InvariantCulture),
                            ["mode"] = _currentSessionGameMode.ToString(),
                            ["theme"] = theme.Name
                        });

                    await ServiceBackbone.SendChatMessage(msg);
                }

                var lobbyVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["User"] = triggeredBy ?? "System",
                    ["Duration"] = duration.ToString(CultureInfo.InvariantCulture),
                    ["GameMode"] = _currentSessionGameMode.ToString(),
                    ["TrackType"] = _currentSessionTrackType.ToString(),
                    ["Laps"] = _currentSessionLaps.ToString(CultureInfo.InvariantCulture),
                    ["Theme"] = theme.Name,
                    ["Game"] = "Racing"
                };

                var lobbyEventArgs = new CommandEventArgs
                {
                    Command = "race",
                    Name = triggeredBy ?? "System",
                    DisplayName = triggeredBy ?? "System"
                };

                try
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                        "race",
                        DefaultCommandEventTypes.RacingLobbyOpened,
                        lobbyEventArgs,
                        lobbyVars);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to fire RacingLobbyOpened trigger");
                }

                return dto;
            }
            finally
            {
                _sessionManager.SessionLock.Release();
            }
        }

        private async Task LaunchHeatAsync(List<PendingDropRecord> racersInHeat, string heatTitle, int heatNumber, int totalHeats, bool isFinal)
        {
            var settings = await settingsService.GetSettingsAsync();
            var rSettings = settings.Racing;
            var theme = await DropGameMediaResolver.ResolveAndPrepareThemeAsync(settings, null, rSettings.ActiveThemeId, emoteService, dropRegistry);
            var sounds = DropGameMediaResolver.ResolveRacingSoundBundle(rSettings, theme, GetMediaUploadService());

            var activeGames = DropGameBroadcastCoordinator.GetActiveGameNames(dropRegistry);
            var isOtherGameActive = activeGames.Any(g => !string.Equals(g, "racing", StringComparison.OrdinalIgnoreCase));

            var racersPayload = racersInHeat.Select((r, idx) => new
            {
                dropToken = r.DropToken,
                username = r.Username,
                displayName = r.DisplayName,
                userColor = r.UserColor,
                emoteUrl = r.Theme.DefaultDropletImageUrl,
                gridPosition = idx + 1
            }).ToList();

            var startPayload = JsonSerializer.Serialize(new
            {
                type = "race_start",
                raceId = Guid.NewGuid().ToString("N"),
                gameMode = _currentSessionGameMode.ToString(),
                trackType = _currentSessionTrackType.ToString(),
                stripPosition = rSettings.StripPosition.ToString(),
                totalLaps = _currentSessionLaps,
                heatTitle,
                heatNumber,
                totalHeats,
                isFinal,
                enableCollisions = rSettings.EnableVehicleCollisions,
                enableDrafting = rSettings.EnableDrafting,
                enableDebris = rSettings.EnableDebrisHazards,
                debrisDensity = rSettings.DebrisDensity,
                enableRubberBanding = rSettings.EnableRubberBanding,
                rubberBandStrength = rSettings.RubberBandStrength,
                baseSpeed = rSettings.BaseSpeed,
                trackAlpha = rSettings.TrackAlpha,
                showTrackBorders = rSettings.ShowTrackBorders,
                trackThicknessPercent = rSettings.TrackThicknessPercent,
                racers = racersPayload,
                theme,
                sounds,
                messages = rSettings.Messages,
                isOtherGameActive,
                activeGames
            }, _jsonOptions);

            await QueueWsMessageAsync(WsTopics.Racing, startPayload, $"Racing race_start dispatch ({heatTitle})");
            await DropGameBroadcastCoordinator.BroadcastOtherDropActiveAsync(webSocketMessenger, logger, WsTopics.Racing, durationSeconds: 25.0);

            var watchdogCts = new CancellationTokenSource();
            lock (_racerLock)
            {
                _heatWatchdogCts?.Cancel();
                _heatWatchdogCts?.Dispose();
                _heatWatchdogCts = watchdogCts;
            }

            var watchdogDuration = TimeSpan.FromSeconds(Math.Max(90, _currentSessionLaps * 60));
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(watchdogDuration, watchdogCts.Token);
                    if (watchdogCts.IsCancellationRequested) return;

                    logger.LogWarning("Racing heat watchdog timed out after {Duration}s. Resetting race state.", watchdogDuration.TotalSeconds);
                    lock (_racerLock)
                    {
                        _isRaceRunning = false;
                        _activeTournament = null;
                        _singleRaceRemainingTokens.Clear();
                        dropRegistry.SetSessionActive(DropGameType.Racing, false);
                    }
                    await StopSessionAsync("HeatTimeout");
                }
                catch (OperationCanceledException)
                {
                    // Watchdog cancelled because heat completed normally
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in racing heat watchdog");
                }
            });

            if (rSettings.AnnounceSessionStatusInChat)
            {
                var template = isFinal ? rSettings.Messages.ChampionshipFinalStart : rSettings.Messages.RaceStart;
                var fallback = isFinal ? RacingMessageSettings.DefaultChampionshipFinalStart : RacingMessageSettings.DefaultRaceStart;
                var startMsg = RacingMessageSettings.Format(template, fallback, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["heat"] = heatTitle,
                    ["count"] = racersInHeat.Count.ToString(CultureInfo.InvariantCulture),
                    ["total"] = totalHeats.ToString(CultureInfo.InvariantCulture),
                    ["heatNumber"] = heatNumber.ToString(CultureInfo.InvariantCulture)
                });
                await ServiceBackbone.SendChatMessage(startMsg);
            }

            var startVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["RacerCount"] = racersInHeat.Count.ToString(CultureInfo.InvariantCulture),
                ["GameMode"] = _currentSessionGameMode.ToString(),
                ["TrackType"] = _currentSessionTrackType.ToString(),
                ["Laps"] = _currentSessionLaps.ToString(CultureInfo.InvariantCulture),
                ["HeatTitle"] = heatTitle,
                ["Theme"] = theme.Name,
                ["Game"] = "Racing"
            };

            var startEventArgs = new CommandEventArgs
            {
                Command = "race",
                Name = "System",
                DisplayName = "System"
            };

            try
            {
                await defaultCommandTriggerService.TriggerDefaultCommandEventAsync("race", DefaultCommandEventTypes.RacingStart, startEventArgs, startVars);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fire RacingStart trigger");
            }
        }

        private async Task OnLobbyTimerExpiredAsync()
        {
            List<PendingDropRecord> allRacers;
            bool shouldLaunch;

            lock (_racerLock)
            {
                shouldLaunch = _lobbyRacers.Count > 0 && !_isRaceRunning;
                if (shouldLaunch)
                {
                    _isRaceRunning = true;
                    allRacers = [.. _lobbyRacers];
                    _lobbyRacers.Clear();
                }
                else
                {
                    allRacers = [];
                }
            }

            if (!shouldLaunch)
            {
                logger.LogInformation("Racing lobby expired with 0 racers. Closing session.");
                await StopSessionAsync("NoRacers");
                return;
            }

            var settings = await settingsService.GetSettingsAsync();
            var rSettings = settings.Racing;
            var maxPerHeat = Math.Max(2, rSettings.MaxRacersPerHeat);

            if (allRacers.Count <= maxPerHeat)
            {
                lock (_racerLock)
                {
                    _activeTournament = null;
                    _singleRaceRemainingTokens.Clear();
                    foreach (var r in allRacers) _singleRaceRemainingTokens.Add(r.DropToken);
                }
                await LaunchHeatAsync(allRacers, "Grand Prix Final", 1, 1, isFinal: true);
            }
            else
            {
                // Multi-heat tournament
                var heatCount = (int)Math.Ceiling((double)allRacers.Count / maxPerHeat);
                var advancePerHeat = Math.Max(1, maxPerHeat / heatCount);

                var heats = new List<List<PendingDropRecord>>();
                for (int h = 0; h < heatCount; h++) heats.Add([]);
                for (int i = 0; i < allRacers.Count; i++)
                {
                    heats[i % heatCount].Add(allRacers[i]);
                }

                lock (_racerLock)
                {
                    _activeTournament = new RacingTournamentState
                    {
                        Heats = heats,
                        CurrentHeatIndex = 0,
                        AdvancePerHeat = advancePerHeat,
                        QualifiedFinalists = [],
                        CurrentHeatFinishedTokens = [],
                        IsChampionshipFinal = false,
                        FinalFinishedTokens = []
                    };
                }

                var currentHeat = heats[0];
                await LaunchHeatAsync(currentHeat, $"Heat 1 of {heatCount} (Top {advancePerHeat} Advance)", 1, heatCount, isFinal: false);
            }
        }

        public async Task LaunchRaceNowAsync(string? triggeredBy = null)
        {
            await _sessionManager.SessionLock.WaitAsync();
            try
            {
                _sessionManager.ExchangeStopSession();
            }
            finally
            {
                _sessionManager.SessionLock.Release();
            }

            await OnLobbyTimerExpiredAsync();
        }

        public async Task<int> AddTestRacersAsync(int count, string? emoteOrVehicle = null, bool autoLaunch = false)
        {
            var settings = await settingsService.GetSettingsAsync();
            var rSettings = settings.Racing;
            if (!rSettings.GameEnabled)
            {
                logger.LogWarning("AddTestRacersAsync rejected because Racing is disabled.");
                return 0;
            }

            if (!_sessionManager.HasActiveTimedSession() && !_isRaceRunning)
            {
                await StartRaceLobbyAsync(triggeredBy: "TestMode");
            }

            var theme = await DropGameMediaResolver.ResolveAndPrepareThemeAsync(settings, null, rSettings.ActiveThemeId, emoteService, dropRegistry);
            var botNames = new[]
            {
                "SpeedyPenguin", "DriftKing", "TurboOtter", "RocketSeal",
                "NitroPuffin", "ApexPredator", "ThunderBeak", "GlacierGlide",
                "BlizzardBolt", "FrostyFury", "PolarProwler", "EchoEcho",
                "VeloPenguin", "HyperHusky", "CometClaw", "SonicSnow",
                "TurboTadpole", "TrackMaster", "SlipstreamSam", "VelocityViper"
            };

            var testColors = new[]
            {
                "#FF5252", "#448AFF", "#69F0AE", "#FFD740",
                "#E040FB", "#FF6E40", "#18FFFF", "#B2FF59",
                "#EEFF41", "#FF4081", "#7C4DFF", "#00E676"
            };

            var added = 0;
            List<PendingDropRecord> lobbySnapshot;

            lock (_racerLock)
            {
                for (int i = 0; i < count; i++)
                {
                    var botName = i < botNames.Length ? botNames[i] : $"BotRacer_{i + 1}";
                    var dropToken = Guid.NewGuid().ToString("N");
                    var mockArgs = new CommandEventArgs
                    {
                        Command = "race",
                        Name = botName.ToLowerInvariant(),
                        DisplayName = botName,
                        UserId = $"test_{Guid.NewGuid():N}"
                    };

                    var record = new PendingDropRecord
                    {
                        DropToken = dropToken,
                        GameType = DropGameType.Racing,
                        Username = mockArgs.Name,
                        DisplayName = mockArgs.DisplayName,
                        UserId = mockArgs.UserId,
                        UserColor = testColors[i % testColors.Length],
                        Theme = theme,
                        EventArgs = mockArgs,
                        CreatedAtUtc = DateTime.UtcNow,
                        EarliestCompletionUtc = DateTime.UtcNow,
                        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
                    };

                    dropRegistry.RegisterDrop(record);
                    _lobbyRacers.Add(record);
                    added++;
                }
                lobbySnapshot = [.. _lobbyRacers];
            }

            var gridPayload = JsonSerializer.Serialize(new
            {
                type = "race_grid_update",
                racers = lobbySnapshot.Select(r => new
                {
                    dropToken = r.DropToken,
                    username = r.Username,
                    displayName = r.DisplayName,
                    userColor = r.UserColor,
                    emoteUrl = r.Theme.DefaultDropletImageUrl
                }).ToList()
            }, _jsonOptions);

            await QueueWsMessageAsync(WsTopics.Racing, gridPayload, "Racing test racers grid update");

            if (autoLaunch)
            {
                await LaunchRaceNowAsync("TestMode");
            }

            return added;
        }

        public async Task<DropGameSessionDto?> StopSessionAsync(string? triggeredBy = null)
        {
            await _sessionManager.SessionLock.WaitAsync();
            try
            {
                var session = _sessionManager.ExchangeStopSession();
                if (session == null) return null;

                dropRegistry.SetSessionActive(DropGameType.Racing, false);
                var activeGames = DropGameBroadcastCoordinator.GetActiveGameNames(dropRegistry);
                var isOtherGameActive = activeGames.Any(g => !string.Equals(g, "racing", StringComparison.OrdinalIgnoreCase));

                var settings = await settingsService.GetSettingsAsync();
                var rSettings = settings.Racing;
                var headerConfig = DropGameHeaderConfigDto.FromSettings(rSettings);

                int completedRacersCount;
                lock (_racerLock)
                {
                    completedRacersCount = _lobbyRacers.Count;
                    _lobbyRacers.Clear();
                    _isRaceRunning = false;
                    _activeTournament = null;
                    _singleRaceRemainingTokens.Clear();
                    _heatWatchdogCts?.Cancel();
                    _heatWatchdogCts?.Dispose();
                    _heatWatchdogCts = null;
                }

                var dto = new DropGameSessionDto
                {
                    GameType = "racing",
                    IsActive = false,
                    TotalDurationSeconds = session.TotalDurationSeconds,
                    RemainingSeconds = 0,
                    StartedAtMs = new DateTimeOffset(session.StartedAtUtc).ToUnixTimeMilliseconds(),
                    EndsAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    TotalDrops = completedRacersCount,
                    IsOtherGameActive = isOtherGameActive,
                    ActiveGames = activeGames,
                    HeaderConfig = headerConfig
                };

                var wsPayload = JsonSerializer.Serialize(new
                {
                    type = "race_end",
                    session = dto,
                    headerConfig,
                    isOtherGameActive,
                    activeGames
                }, _jsonOptions);

                await QueueWsMessageAsync(WsTopics.Racing, wsPayload, "Racing race_end");
                await DropGameBroadcastCoordinator.BroadcastOtherGameStatusAsync(webSocketMessenger, logger, WsTopics.Racing, "racing", isOtherGameActive: false, activeGames: activeGames);

                if (!string.Equals(triggeredBy, "NoRacers", StringComparison.OrdinalIgnoreCase))
                {
                    if (rSettings.AnnounceSessionStatusInChat)
                    {
                        var endMsg = RacingMessageSettings.Format(
                            rSettings.Messages.SessionEnd,
                            RacingMessageSettings.DefaultSessionEnd,
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["count"] = completedRacersCount.ToString(CultureInfo.InvariantCulture),
                                ["duration"] = rSettings.LobbyDurationSeconds.ToString(CultureInfo.InvariantCulture)
                            });
                        await ServiceBackbone.SendChatMessage(endMsg);
                    }
                }

                return dto;
            }
            finally
            {
                _sessionManager.SessionLock.Release();
            }
        }

        public async Task<string> TriggerJoinRaceAsync(
            CommandEventArgs eventArgs,
            string? emoteOrVehicle = null,
            string? themeOverrideId = null,
            bool allowDirectUrlOrPath = true)
        {
            ArgumentNullException.ThrowIfNull(eventArgs);
            var settings = await settingsService.GetSettingsAsync();
            var rSettings = settings.Racing;
            if (!rSettings.GameEnabled)
                throw new InvalidOperationException("Racing Minigame is disabled.");

            // If no lobby active: auto-start a lobby if configured
            if (!_sessionManager.HasActiveTimedSession() || _isRaceRunning)
            {
                if (rSettings.AutoStartOnCommand && !_isRaceRunning)
                {
                    await StartRaceLobbyAsync(rSettings.LobbyDurationSeconds, themeOverrideId, triggeredBy: eventArgs.DisplayName);
                }
                else
                {
                    if (_isRaceRunning)
                    {
                        var msg = RacingMessageSettings.Format(
                            rSettings.Messages.RaceInProgress,
                            RacingMessageSettings.DefaultRaceInProgress,
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["user"] = eventArgs.DisplayName
                            });
                        await ServiceBackbone.SendChatMessage(msg);
                    }
                    else
                    {
                        var msg = RacingMessageSettings.Format(
                            rSettings.Messages.NoLobbyOpen,
                            RacingMessageSettings.DefaultNoLobbyOpen,
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["user"] = eventArgs.DisplayName
                            });
                        await ServiceBackbone.SendChatMessage(msg);
                    }
                    return string.Empty;
                }
            }

            lock (_racerLock)
            {
                if (_lobbyRacers.Count >= rSettings.MaxLobbyRacers)
                {
                    var msg = RacingMessageSettings.Format(
                        rSettings.Messages.GridFull,
                        RacingMessageSettings.DefaultGridFull,
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["user"] = eventArgs.DisplayName,
                            ["max"] = rSettings.MaxLobbyRacers.ToString(CultureInfo.InvariantCulture)
                        });
                    _ = ServiceBackbone.SendChatMessage(msg);
                    return string.Empty;
                }

                // Check if user already joined
                if (_lobbyRacers.Any(r => string.Equals(r.UserId, eventArgs.UserId, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!string.IsNullOrWhiteSpace(rSettings.Messages.AlreadyJoined))
                    {
                        var msg = RacingMessageSettings.Format(
                            rSettings.Messages.AlreadyJoined,
                            RacingMessageSettings.DefaultAlreadyJoined,
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["user"] = eventArgs.DisplayName
                            });
                        _ = ServiceBackbone.SendChatMessage(msg);
                    }
                    return string.Empty;
                }
            }

            var theme = await DropGameMediaResolver.ResolveAndPrepareThemeAsync(settings, themeOverrideId, rSettings.ActiveThemeId, emoteService, dropRegistry);
            var resolvedEmoteUrl = await DropGameMediaResolver.ResolveCharacterOrEmoteUrlAsync(
                emoteService,
                eventArgs,
                emoteOrVehicle,
                theme.DefaultDropletImageUrl,
                allowDirectUrlOrPath: allowDirectUrlOrPath);

            var userColor = _chatColorService.GetOrAssignColor(eventArgs.UserId, eventArgs.Color);
            if (string.IsNullOrWhiteSpace(userColor)) userColor = "#FFDD57";

            var racerTheme = theme.Clone();
            if (!string.IsNullOrWhiteSpace(resolvedEmoteUrl))
            {
                racerTheme.DefaultDropletImageUrl = resolvedEmoteUrl;
            }

            var dropToken = Guid.NewGuid().ToString("N");
            var record = new PendingDropRecord
            {
                DropToken = dropToken,
                GameType = DropGameType.Racing,
                Username = eventArgs.Name,
                DisplayName = eventArgs.DisplayName,
                UserId = eventArgs.UserId,
                UserColor = userColor,
                EventArgs = eventArgs,
                Theme = racerTheme,
                RacingMode = _currentSessionGameMode.ToString(),
                CreatedAtUtc = DateTime.UtcNow,
                EarliestCompletionUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
            };

            lock (_racerLock)
            {
                if (_lobbyRacers.Count >= rSettings.MaxLobbyRacers)
                {
                    var msg = RacingMessageSettings.Format(
                        rSettings.Messages.GridFull,
                        RacingMessageSettings.DefaultGridFull,
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["user"] = eventArgs.DisplayName,
                            ["max"] = rSettings.MaxLobbyRacers.ToString(CultureInfo.InvariantCulture)
                        });
                    _ = ServiceBackbone.SendChatMessage(msg);
                    return string.Empty;
                }

                if (_lobbyRacers.Any(r => string.Equals(r.UserId, eventArgs.UserId, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!string.IsNullOrWhiteSpace(rSettings.Messages.AlreadyJoined))
                    {
                        var msg = RacingMessageSettings.Format(
                            rSettings.Messages.AlreadyJoined,
                            RacingMessageSettings.DefaultAlreadyJoined,
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["user"] = eventArgs.DisplayName
                            });
                        _ = ServiceBackbone.SendChatMessage(msg);
                    }
                    return string.Empty;
                }

                dropRegistry.RegisterDrop(record);
                _lobbyRacers.Add(record);
            }

            _sessionManager.RecordUserDrop(eventArgs.UserId);

            var updatePayload = JsonSerializer.Serialize(new
            {
                type = "race_grid_update",
                racerCount = _lobbyRacers.Count,
                maxRacers = rSettings.MaxLobbyRacers,
                newRacer = new
                {
                    dropToken = record.DropToken,
                    username = record.Username,
                    displayName = record.DisplayName,
                    userColor = record.UserColor,
                    emoteUrl = record.Theme.DefaultDropletImageUrl,
                    gridPosition = _lobbyRacers.Count
                }
            }, _jsonOptions);

            await QueueWsMessageAsync(WsTopics.Racing, updatePayload, $"Racing grid join for {eventArgs.DisplayName}");

            if (!string.IsNullOrWhiteSpace(rSettings.Messages.UserJoined))
            {
                var msg = RacingMessageSettings.Format(
                    rSettings.Messages.UserJoined,
                    RacingMessageSettings.DefaultUserJoined,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["user"] = eventArgs.DisplayName,
                        ["position"] = _lobbyRacers.Count.ToString(CultureInfo.InvariantCulture)
                    });
                _ = ServiceBackbone.SendChatMessage(msg);
            }

            return dropToken;
        }

        public async Task<string> TriggerSoloTimeTrialAsync(
            CommandEventArgs eventArgs,
            string? emoteOrVehicle = null,
            string? themeOverrideId = null,
            bool allowDirectUrlOrPath = true)
        {
            ArgumentNullException.ThrowIfNull(eventArgs);
            var settings = await settingsService.GetSettingsAsync();
            var rSettings = settings.Racing;
            if (!rSettings.GameEnabled)
                throw new InvalidOperationException("Racing Minigame is disabled.");

            if (_isRaceRunning || _sessionManager.HasActiveTimedSession())
            {
                var msg = RacingMessageSettings.Format(
                    rSettings.Messages.RaceInProgress,
                    RacingMessageSettings.DefaultRaceInProgress,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["user"] = eventArgs.DisplayName
                    });
                await ServiceBackbone.SendChatMessage(msg);
                return string.Empty;
            }

            var theme = await DropGameMediaResolver.ResolveAndPrepareThemeAsync(settings, themeOverrideId, rSettings.ActiveThemeId, emoteService, dropRegistry);
            var resolvedEmoteUrl = await DropGameMediaResolver.ResolveCharacterOrEmoteUrlAsync(
                emoteService,
                eventArgs,
                emoteOrVehicle,
                theme.DefaultDropletImageUrl,
                allowDirectUrlOrPath: allowDirectUrlOrPath);

            var userColor = _chatColorService.GetOrAssignColor(eventArgs.UserId, eventArgs.Color);
            if (string.IsNullOrWhiteSpace(userColor)) userColor = "#00D2D3";

            var racerTheme = theme.Clone();
            if (!string.IsNullOrWhiteSpace(resolvedEmoteUrl))
            {
                racerTheme.DefaultDropletImageUrl = resolvedEmoteUrl;
            }

            var dropToken = Guid.NewGuid().ToString("N");
            var record = new PendingDropRecord
            {
                DropToken = dropToken,
                GameType = DropGameType.Racing,
                Username = eventArgs.Name,
                DisplayName = eventArgs.DisplayName,
                UserId = eventArgs.UserId,
                UserColor = userColor,
                EventArgs = eventArgs,
                Theme = racerTheme,
                RacingMode = "TimeTrial",
                CreatedAtUtc = DateTime.UtcNow,
                EarliestCompletionUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
            };

            dropRegistry.RegisterDrop(record);

            var sounds = DropGameMediaResolver.ResolveRacingSoundBundle(rSettings, theme, GetMediaUploadService());
            var activeGames = DropGameBroadcastCoordinator.GetActiveGameNames(dropRegistry);
            var isOtherGameActive = activeGames.Any(g => !string.Equals(g, "racing", StringComparison.OrdinalIgnoreCase));

            var timeTrialPayload = JsonSerializer.Serialize(new
            {
                type = "timetrial_start",
                dropToken = record.DropToken,
                username = record.Username,
                displayName = record.DisplayName,
                userColor = record.UserColor,
                emoteUrl = record.Theme.DefaultDropletImageUrl,
                trackType = rSettings.TrackType.ToString(),
                stripPosition = rSettings.StripPosition.ToString(),
                totalLaps = 1,
                enableDebris = rSettings.EnableDebrisHazards,
                debrisDensity = rSettings.DebrisDensity,
                baseSpeed = rSettings.BaseSpeed,
                trackAlpha = rSettings.TrackAlpha,
                showTrackBorders = rSettings.ShowTrackBorders,
                trackThicknessPercent = rSettings.TrackThicknessPercent,
                theme,
                sounds,
                isOtherGameActive,
                activeGames
            }, _jsonOptions);

            await QueueWsMessageAsync(WsTopics.Racing, timeTrialPayload, $"Racing timetrial_start for {eventArgs.DisplayName}");
            await DropGameBroadcastCoordinator.BroadcastOtherDropActiveAsync(webSocketMessenger, logger, WsTopics.Racing, durationSeconds: 15.0);

            return dropToken;
        }

        private async Task AwardRacePlacementPointsAndTriggers(PendingDropRecord record, int placement, double lapTime, int wreckCount, RacingGameSettings rSettings)
        {
            var pointsAwarded = placement switch
            {
                1 => rSettings.WinPointsFirst,
                2 => rSettings.WinPointsSecond,
                3 => rSettings.WinPointsThird,
                _ => rSettings.ParticipationPoints
            };

            if (pointsAwarded > 0 && _pointsSystem != null)
            {
                try
                {
                    await _pointsSystem.AddPointsByUserIdAndGame(record.UserId, "Racing", pointsAwarded);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to award {Points} points to {User}", pointsAwarded, record.DisplayName);
                }
            }

            var actionVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["User"] = record.Username,
                ["DisplayName"] = record.DisplayName,
                ["UserId"] = record.UserId,
                ["Placement"] = placement.ToString(CultureInfo.InvariantCulture),
                ["Score"] = record.Score.ToString(CultureInfo.InvariantCulture),
                ["PointsAwarded"] = pointsAwarded.ToString(CultureInfo.InvariantCulture),
                ["LapTime"] = lapTime.ToString("F2", CultureInfo.InvariantCulture),
                ["WreckCount"] = wreckCount.ToString(CultureInfo.InvariantCulture),
                ["RacingMode"] = record.RacingMode ?? "GrandPrix",
                ["Theme"] = record.Theme.Name,
                ["Game"] = "Racing"
            };

            try
            {
                if (placement == 1)
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync("race", DefaultCommandEventTypes.RacingWin, record.EventArgs, actionVars);
                    if (rSettings.AnnounceSessionStatusInChat)
                    {
                        var winMsg = RacingMessageSettings.Format(
                            rSettings.Messages.RaceWin,
                            RacingMessageSettings.DefaultRaceWin,
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["user"] = record.DisplayName,
                                ["time"] = lapTime.ToString("F2", CultureInfo.InvariantCulture),
                                ["points"] = pointsAwarded.ToString(CultureInfo.InvariantCulture)
                            });
                        await ServiceBackbone.SendChatMessage(winMsg);
                    }
                }
                else if (placement <= 3)
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync("race", DefaultCommandEventTypes.RacingPodium, record.EventArgs, actionVars);
                }

                if (wreckCount > 0)
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync("race", DefaultCommandEventTypes.RacingWreck, record.EventArgs, actionVars);
                }

                await defaultCommandTriggerService.TriggerDefaultCommandEventAsync("race", DefaultCommandEventTypes.RacingEnd, record.EventArgs, actionVars);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fire Racing DefaultCommandEvent for {User}", record.DisplayName);
            }
        }

        public async Task ProcessCompletionAsync(PendingDropRecord record)
        {
            var settings = await settingsService.GetSettingsAsync();
            var rSettings = settings.Racing;

            var placement = record.RacePlacement ?? 1;
            var lapTime = record.LapTimeSeconds ?? 0.0;
            var wreckCount = record.WreckCount ?? 0;
            var isTimeTrial = string.Equals(record.RacingMode, "TimeTrial", StringComparison.OrdinalIgnoreCase);

            if (isTimeTrial)
            {
                var actionVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["User"] = record.Username,
                    ["DisplayName"] = record.DisplayName,
                    ["UserId"] = record.UserId,
                    ["Placement"] = "1",
                    ["LapTime"] = lapTime.ToString("F2", CultureInfo.InvariantCulture),
                    ["WreckCount"] = wreckCount.ToString(CultureInfo.InvariantCulture),
                    ["RacingMode"] = "TimeTrial",
                    ["Theme"] = record.Theme.Name,
                    ["Game"] = "Racing"
                };

                try
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync("timetrial", DefaultCommandEventTypes.RacingTimeTrialComplete, record.EventArgs, actionVars);
                    if (rSettings.AnnounceSessionStatusInChat)
                    {
                        var ttMsg = RacingMessageSettings.Format(
                            rSettings.Messages.TimeTrialComplete,
                            RacingMessageSettings.DefaultTimeTrialComplete,
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["user"] = record.DisplayName,
                                ["time"] = lapTime.ToString("F2", CultureInfo.InvariantCulture),
                                ["wrecks"] = wreckCount.ToString(CultureInfo.InvariantCulture)
                            });
                        await ServiceBackbone.SendChatMessage(ttMsg);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to fire RacingTimeTrialComplete trigger for {User}", record.DisplayName);
                }
                return;
            }

            // Tournament vs Single Race
            RacingTournamentState? tournament;
            lock (_racerLock)
            {
                tournament = _activeTournament;
            }

            if (tournament == null)
            {
                // Single race final
                await AwardRacePlacementPointsAndTriggers(record, placement, lapTime, wreckCount, rSettings);

                lock (_racerLock)
                {
                    _singleRaceRemainingTokens.Remove(record.DropToken);
                    if (_singleRaceRemainingTokens.Count == 0)
                    {
                        _isRaceRunning = false;
                        _heatWatchdogCts?.Cancel();
                        dropRegistry.SetSessionActive(DropGameType.Racing, false);
                    }
                }
                return;
            }

            // Active Tournament Heats
            if (!tournament.IsChampionshipFinal)
            {
                List<PendingDropRecord> currentHeatRacers;
                int heatNum;
                int totalHeats;
                bool heatFinished = false;

                lock (_racerLock)
                {
                    currentHeatRacers = tournament.Heats[tournament.CurrentHeatIndex];
                    tournament.CurrentHeatFinishedTokens.Add(record.DropToken);

                    if (placement <= tournament.AdvancePerHeat)
                    {
                        tournament.QualifiedFinalists.Add(record);
                    }

                    heatNum = tournament.CurrentHeatIndex + 1;
                    totalHeats = tournament.TotalHeats;

                    if (tournament.CurrentHeatFinishedTokens.Count >= currentHeatRacers.Count)
                    {
                        heatFinished = true;
                    }
                }

                // Award participation points to non-qualifiers
                if (placement > tournament.AdvancePerHeat && rSettings.ParticipationPoints > 0 && _pointsSystem != null)
                {
                    try { await _pointsSystem.AddPointsByUserIdAndGame(record.UserId, "Racing", rSettings.ParticipationPoints); } catch { }
                }

                if (heatFinished)
                {
                    lock (_racerLock)
                    {
                        _heatWatchdogCts?.Cancel();
                    }
                    string advancingNames;
                    lock (_racerLock)
                    {
                        advancingNames = string.Join(", ", tournament.QualifiedFinalists
                            .TakeLast(Math.Min(tournament.AdvancePerHeat, currentHeatRacers.Count))
                            .Select(r => r.DisplayName));

                        tournament.CurrentHeatIndex++;
                        tournament.CurrentHeatFinishedTokens.Clear();
                    }

                    if (rSettings.AnnounceSessionStatusInChat)
                    {
                        var heatMsg = RacingMessageSettings.Format(
                            rSettings.Messages.HeatComplete,
                            RacingMessageSettings.DefaultHeatComplete,
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["heat"] = heatNum.ToString(CultureInfo.InvariantCulture),
                                ["total"] = totalHeats.ToString(CultureInfo.InvariantCulture),
                                ["qualifiers"] = advancingNames
                            });
                        await ServiceBackbone.SendChatMessage(heatMsg);
                    }

                    if (tournament.CurrentHeatIndex < tournament.TotalHeats)
                    {
                        // Launch next preliminary heat after 6-second intermission
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(6000);
                            if (!_isRaceRunning || _activeTournament == null) return;
                            List<PendingDropRecord> nextHeat;
                            int nextNum;
                            lock (_racerLock)
                            {
                                nextHeat = _activeTournament.Heats[_activeTournament.CurrentHeatIndex];
                                nextNum = _activeTournament.CurrentHeatIndex + 1;
                            }
                            await LaunchHeatAsync(nextHeat, $"Heat {nextNum} of {totalHeats} (Top {_activeTournament.AdvancePerHeat} Advance)", nextNum, totalHeats, isFinal: false);
                        });
                    }
                    else
                    {
                        // Launch Championship Final after 6-second intermission
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(6000);
                            if (!_isRaceRunning || _activeTournament == null) return;

                            List<PendingDropRecord> finalRacers = [];
                            lock (_racerLock)
                            {
                                _activeTournament.IsChampionshipFinal = true;
                                foreach (var finalist in _activeTournament.QualifiedFinalists)
                                {
                                    var newDropToken = Guid.NewGuid().ToString("N");
                                    var newRecord = new PendingDropRecord
                                    {
                                        DropToken = newDropToken,
                                        GameType = DropGameType.Racing,
                                        Username = finalist.Username,
                                        DisplayName = finalist.DisplayName,
                                        UserId = finalist.UserId,
                                        UserColor = finalist.UserColor,
                                        Theme = finalist.Theme,
                                        EventArgs = finalist.EventArgs,
                                        CreatedAtUtc = DateTime.UtcNow,
                                        EarliestCompletionUtc = DateTime.UtcNow,
                                        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
                                    };
                                    dropRegistry.RegisterDrop(newRecord);
                                    finalRacers.Add(newRecord);
                                }
                                _activeTournament.FinalFinishedTokens = [.. finalRacers.Select(f => f.DropToken)];
                            }

                            await LaunchHeatAsync(finalRacers, "🏆 Championship Final", totalHeats + 1, totalHeats, isFinal: true);
                        });
                    }
                }
            }
            else
            {
                // Championship Final completion
                await AwardRacePlacementPointsAndTriggers(record, placement, lapTime, wreckCount, rSettings);

                lock (_racerLock)
                {
                    tournament.FinalFinishedTokens.Remove(record.DropToken);
                    if (tournament.FinalFinishedTokens.Count == 0)
                    {
                        _isRaceRunning = false;
                        _activeTournament = null;
                        _heatWatchdogCts?.Cancel();
                        dropRegistry.SetSessionActive(DropGameType.Racing, false);
                    }
                }
            }
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;

            var cmdName = command.CommandProperties.CommandName;
            var settings = await settingsService.GetSettingsAsync();
            var rSettings = settings.Racing;

            if (cmdName.Equals("timetrial", StringComparison.OrdinalIgnoreCase) ||
                cmdName.Equals("tt", StringComparison.OrdinalIgnoreCase))
            {
                var emoteInput = e.Args.Count > 0 ? e.Args[0] : (e.Arg ?? "").Trim();
                await TriggerSoloTimeTrialAsync(e, emoteInput, allowDirectUrlOrPath: true);
                return;
            }

            if (cmdName.Equals("race", StringComparison.OrdinalIgnoreCase) ||
                cmdName.Equals("racing", StringComparison.OrdinalIgnoreCase) ||
                cmdName.Equals("kart", StringComparison.OrdinalIgnoreCase) ||
                cmdName.Equals("grandprix", StringComparison.OrdinalIgnoreCase))
            {
                var firstArg = e.Args.Count > 0 ? e.Args[0] : (e.Arg ?? "").Trim();

                if (e.IsModOrHigher() && firstArg.Equals("start", StringComparison.OrdinalIgnoreCase))
                {
                    int? customDuration = null;
                    if (e.Args.Count > 1 && int.TryParse(e.Args[1], out var parsedDuration))
                    {
                        customDuration = parsedDuration;
                    }
                    if (!rSettings.GameEnabled)
                    {
                        logger.LogInformation("Ignored !race start from {User} because Racing is disabled", e.DisplayName);
                        return;
                    }
                    await StartRaceLobbyAsync(customDuration, triggeredBy: e.DisplayName);
                    return;
                }

                if (e.IsModOrHigher() && (firstArg.Equals("stop", StringComparison.OrdinalIgnoreCase) || firstArg.Equals("cancel", StringComparison.OrdinalIgnoreCase)))
                {
                    await StopSessionAsync(triggeredBy: e.DisplayName);
                    return;
                }

                // Normal viewer command: join active or auto-started race lobby
                await TriggerJoinRaceAsync(e, firstArg, allowDirectUrlOrPath: true);
            }
        }

        public void Dispose()
        {
            lock (_racerLock)
            {
                _heatWatchdogCts?.Cancel();
                _heatWatchdogCts?.Dispose();
                _heatWatchdogCts = null;
            }
            _sessionManager.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
