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
        private readonly List<RacerFinalResult> _singleRaceResults = [];
        private readonly Dictionary<string, PendingDropRecord> _activeRaceDropRecords = [];
        private bool _singleRaceAnnounced = false;
        private CancellationTokenSource? _heatWatchdogCts;

        public sealed record RacerFinalResult(
            string Username,
            string DisplayName,
            int Placement,
            double LapTimeSeconds,
            int KnockoutCount,
            bool IsTotaled
        );

        public class RacingTournamentState
        {
            public List<List<PendingDropRecord>> Heats { get; set; } = [];
            public int CurrentHeatIndex { get; set; } = 0;
            public int AdvancePerHeat { get; set; } = 2;
            public List<PendingDropRecord> QualifiedFinalists { get; set; } = [];
            public HashSet<string> CurrentHeatFinishedTokens { get; set; } = [];
            public bool IsChampionshipFinal { get; set; } = false;
            public HashSet<string> FinalFinishedTokens { get; set; } = [];
            public List<RacerFinalResult> FinalResults { get; } = [];
            public bool FinalAnnounced { get; set; } = false;
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
                    _activeTournament = null;
                    _singleRaceRemainingTokens.Clear();
                    _singleRaceResults.Clear();
                    _activeRaceDropRecords.Clear();
                    _singleRaceAnnounced = false;
                    _heatWatchdogCts?.Cancel();
                    _heatWatchdogCts?.Dispose();
                    _heatWatchdogCts = null;
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
                    enableVehicleDamage = rSettings.EnableVehicleDamage || _currentSessionGameMode == RacingGameMode.DestructionDerby,
                    enableItemPickups = rSettings.EnableItemPickups,
                    startingHealth = rSettings.StartingHealth,
                    ramDamageMultiplier = rSettings.RamDamageMultiplier,
                    derbyKnockoutPoints = rSettings.DerbyKnockoutPoints,
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

            var isDerby = _currentSessionGameMode == RacingGameMode.DestructionDerby;
            var heatLaps = _currentSessionGameMode == RacingGameMode.Elimination
                ? Math.Max(1, racersInHeat.Count - 1)
                : _currentSessionLaps;

            var startPayload = JsonSerializer.Serialize(new
            {
                type = "race_start",
                raceId = Guid.NewGuid().ToString("N"),
                gameMode = _currentSessionGameMode.ToString(),
                trackType = _currentSessionTrackType.ToString(),
                stripPosition = rSettings.StripPosition.ToString(),
                totalLaps = heatLaps,
                heatTitle,
                heatNumber,
                totalHeats,
                isFinal,
                enableCollisions = rSettings.EnableVehicleCollisions,
                enableDrafting = rSettings.EnableDrafting,
                enableDebris = rSettings.EnableDebrisHazards,
                debrisDensity = rSettings.DebrisDensity,
                enableRubberBanding = !isDerby && rSettings.EnableRubberBanding,
                rubberBandStrength = rSettings.RubberBandStrength,
                baseSpeed = rSettings.BaseSpeed,
                trackAlpha = rSettings.TrackAlpha,
                showTrackBorders = rSettings.ShowTrackBorders,
                trackThicknessPercent = rSettings.TrackThicknessPercent,
                enableVehicleDamage = rSettings.EnableVehicleDamage || isDerby,
                enableItemPickups = rSettings.EnableItemPickups,
                startingHealth = rSettings.StartingHealth,
                ramDamageMultiplier = rSettings.RamDamageMultiplier,
                derbyKnockoutPoints = rSettings.DerbyKnockoutPoints,
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

            var speedScale = Math.Max(0.2, rSettings.BaseSpeed);
            var baseDuration = isDerby ? 120.0 : (heatLaps * 60.0);
            var calculatedSeconds = baseDuration * (0.5 / speedScale);
            var watchdogDuration = TimeSpan.FromSeconds(Math.Max(90, (int)Math.Ceiling(calculatedSeconds)));
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(watchdogDuration, watchdogCts.Token);
                    if (watchdogCts.IsCancellationRequested) return;

                    bool shouldAnnounceTournament = false;
                    List<RacerFinalResult> tournamentResults = [];
                    bool shouldAnnounceSingle = false;
                    List<RacerFinalResult> singleResults = [];

                    lock (_racerLock)
                    {
                        if (_heatWatchdogCts != watchdogCts) return;

                        if (_activeTournament != null && _activeTournament.IsChampionshipFinal && !_activeTournament.FinalAnnounced && _activeTournament.FinalResults.Count > 0)
                        {
                            _activeTournament.FinalAnnounced = true;
                            shouldAnnounceTournament = true;
                            tournamentResults = [.. _activeTournament.FinalResults.OrderBy(r => r.Placement).ThenBy(r => r.LapTimeSeconds)];
                        }
                        else if (_activeTournament == null && !_singleRaceAnnounced && _singleRaceResults.Count > 0)
                        {
                            _singleRaceAnnounced = true;
                            shouldAnnounceSingle = true;
                            singleResults = [.. _singleRaceResults.OrderBy(r => r.Placement).ThenBy(r => r.LapTimeSeconds)];
                        }

                        _isRaceRunning = false;
                        _activeTournament = null;
                        _singleRaceRemainingTokens.Clear();
                        _singleRaceResults.Clear();
                        _singleRaceAnnounced = false;
                        _heatWatchdogCts = null;
                        dropRegistry.SetSessionActive(DropGameType.Racing, false);
                    }

                    if (shouldAnnounceTournament)
                    {
                        await AnnounceRaceResultsAsync(tournamentResults, isTournamentFinal: true, rSettings);
                    }
                    else if (shouldAnnounceSingle)
                    {
                        await AnnounceRaceResultsAsync(singleResults, isTournamentFinal: false, rSettings);
                    }

                    logger.LogWarning("Racing heat watchdog timed out after {Duration}s. Resetting race state.", watchdogDuration.TotalSeconds);
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
                ["Laps"] = heatLaps.ToString(CultureInfo.InvariantCulture),
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

            var isElimination = _currentSessionGameMode == RacingGameMode.Elimination;
            var isTimeTrial = _currentSessionGameMode == RacingGameMode.TimeTrial;
            var isDerby = _currentSessionGameMode == RacingGameMode.DestructionDerby;

            if (allRacers.Count <= maxPerHeat)
            {
                lock (_racerLock)
                {
                    _activeTournament = null;
                    _singleRaceRemainingTokens.Clear();
                    _singleRaceResults.Clear();
                    _activeRaceDropRecords.Clear();
                    _singleRaceAnnounced = false;
                    foreach (var r in allRacers) _singleRaceRemainingTokens.Add(r.DropToken);
                }
                var singleTitle = isDerby ? "Demolition Derby Final" : (isElimination ? "Elimination Final" : (isTimeTrial ? "Time Trial" : "Grand Prix Final"));
                await LaunchHeatAsync(allRacers, singleTitle, 1, 1, isFinal: true);
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
                var heatTitle = isDerby
                    ? $"Demolition Derby Heat 1 of {heatCount} (Top {advancePerHeat} Survivors Advance)"
                    : (isElimination
                        ? $"Elimination Heat 1 of {heatCount} (Top {advancePerHeat} Advance)"
                        : $"Heat 1 of {heatCount} (Top {advancePerHeat} Advance)");
                await LaunchHeatAsync(currentHeat, heatTitle, 1, heatCount, isFinal: false);
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

        public async Task<int> AddTestRacersAsync(
            int count,
            string? emoteOrVehicle = null,
            bool autoLaunch = false,
            RacingGameMode? gameModeOverride = null,
            RacingTrackType? trackTypeOverride = null)
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
                await StartRaceLobbyAsync(
                    triggeredBy: "TestMode",
                    gameModeOverride: gameModeOverride,
                    trackTypeOverride: trackTypeOverride);
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

                int completedRacersCount;
                bool hadRaceState;
                lock (_racerLock)
                {
                    hadRaceState = _isRaceRunning || _activeTournament != null || _singleRaceRemainingTokens.Count > 0 || _lobbyRacers.Count > 0;
                    completedRacersCount = _lobbyRacers.Count;
                    _lobbyRacers.Clear();
                    _isRaceRunning = false;
                    _activeTournament = null;
                    _singleRaceRemainingTokens.Clear();
                    _singleRaceResults.Clear();
                    _activeRaceDropRecords.Clear();
                    _singleRaceAnnounced = false;
                    _heatWatchdogCts?.Cancel();
                    _heatWatchdogCts?.Dispose();
                    _heatWatchdogCts = null;
                }

                if (session == null && !hadRaceState && !string.Equals(triggeredBy, "HeatTimeout", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                dropRegistry.SetSessionActive(DropGameType.Racing, false);
                var activeGames = DropGameBroadcastCoordinator.GetActiveGameNames(dropRegistry);
                var isOtherGameActive = activeGames.Any(g => !string.Equals(g, "racing", StringComparison.OrdinalIgnoreCase));

                var settings = await settingsService.GetSettingsAsync();
                var rSettings = settings.Racing;
                var headerConfig = DropGameHeaderConfigDto.FromSettings(rSettings);

                var totalDuration = session?.TotalDurationSeconds ?? rSettings.LobbyDurationSeconds;
                var startedAtMs = session != null
                    ? new DateTimeOffset(session.StartedAtUtc).ToUnixTimeMilliseconds()
                    : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                var dto = new DropGameSessionDto
                {
                    GameType = "racing",
                    IsActive = false,
                    TotalDurationSeconds = totalDuration,
                    RemainingSeconds = 0,
                    StartedAtMs = startedAtMs,
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

                if (!string.Equals(triggeredBy, "NoRacers", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(triggeredBy, "HeatTimeout", StringComparison.OrdinalIgnoreCase))
                {
                    if (rSettings.AnnounceSessionStatusInChat)
                    {
                        var endMsg = RacingMessageSettings.Format(
                            rSettings.Messages.SessionEnd,
                            RacingMessageSettings.DefaultSessionEnd,
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["count"] = completedRacersCount.ToString(CultureInfo.InvariantCulture),
                                ["duration"] = totalDuration.ToString(CultureInfo.InvariantCulture)
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

            var currentSession = GetCurrentSession();
            var capturedStartedAt = currentSession?.StartedAtMs;

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

            int gridPosition;
            lock (_racerLock)
            {
                var activeSession = GetCurrentSession();
                if (_isRaceRunning || !_sessionManager.HasActiveTimedSession() || activeSession == null || activeSession.StartedAtMs != capturedStartedAt)
                {
                    logger.LogInformation("Lobby state changed while resolving racer media for {User}. Join rejected.", eventArgs.DisplayName);
                    var msg = RacingMessageSettings.Format(
                        _isRaceRunning ? rSettings.Messages.RaceInProgress : rSettings.Messages.NoLobbyOpen,
                        _isRaceRunning ? RacingMessageSettings.DefaultRaceInProgress : RacingMessageSettings.DefaultNoLobbyOpen,
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["user"] = eventArgs.DisplayName
                        });
                    _ = ServiceBackbone.SendChatMessage(msg);
                    return string.Empty;
                }

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
                gridPosition = _lobbyRacers.Count;
            }

            _sessionManager.RecordUserDrop(eventArgs.UserId);

            var updatePayload = JsonSerializer.Serialize(new
            {
                type = "race_grid_update",
                racerCount = gridPosition,
                maxRacers = rSettings.MaxLobbyRacers,
                newRacer = new
                {
                    dropToken = record.DropToken,
                    username = record.Username,
                    displayName = record.DisplayName,
                    userColor = record.UserColor,
                    emoteUrl = record.Theme.DefaultDropletImageUrl,
                    gridPosition = gridPosition
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
            var isDerby = _currentSessionGameMode == RacingGameMode.DestructionDerby;
            var pointsAwarded = placement switch
            {
                1 => rSettings.WinPointsFirst,
                2 => rSettings.WinPointsSecond,
                3 => rSettings.WinPointsThird,
                _ => rSettings.ParticipationPoints
            };

            // Bonus points for knockouts in Derby / Combat Racing
            var koBonus = (record.KnockoutCount ?? 0) * rSettings.DerbyKnockoutPoints;
            pointsAwarded += koBonus;

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
                ["KnockoutCount"] = (record.KnockoutCount ?? 0).ToString(CultureInfo.InvariantCulture),
                ["IsTotaled"] = record.IsTotaled ? "True" : "False",
                ["RacingMode"] = record.RacingMode ?? "GrandPrix",
                ["Theme"] = record.Theme.Name,
                ["Game"] = "Racing"
            };

            try
            {
                if (record.KnockoutCount > 0)
                {
                    List<string> victimNames;
                    lock (_racerLock)
                    {
                        victimNames = _activeRaceDropRecords.Values
                            .Where(r => string.Equals(r.KillerDropToken, record.DropToken, StringComparison.OrdinalIgnoreCase))
                            .Select(r => r.DisplayName)
                            .Distinct()
                            .ToList();
                    }
                    var victimStr = victimNames.Count > 0 ? string.Join(", ", victimNames) : "Opponent";

                    var knockoutVars = new Dictionary<string, string>(actionVars, StringComparer.OrdinalIgnoreCase)
                    {
                        ["Attacker"] = record.DisplayName,
                        ["Victim"] = victimStr,
                        ["PointsAwarded"] = koBonus.ToString(CultureInfo.InvariantCulture)
                    };

                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync("race", DefaultCommandEventTypes.RacingDerbyKnockout, record.EventArgs, knockoutVars);
                }

                if (placement == 1)
                {
                    await defaultCommandTriggerService.TriggerDefaultCommandEventAsync("race", DefaultCommandEventTypes.RacingWin, record.EventArgs, actionVars);
                    if (rSettings.AnnounceSessionStatusInChat)
                    {
                        var winTemplate = isDerby ? rSettings.Messages.DerbyChampion : rSettings.Messages.RaceWin;
                        var defaultTemplate = isDerby ? RacingMessageSettings.DefaultDerbyChampion : RacingMessageSettings.DefaultRaceWin;
                        var winMsg = RacingMessageSettings.Format(
                            winTemplate,
                            defaultTemplate,
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["user"] = record.DisplayName,
                                ["time"] = lapTime.ToString("F2", CultureInfo.InvariantCulture),
                                ["kills"] = (record.KnockoutCount ?? 0).ToString(CultureInfo.InvariantCulture),
                                ["points"] = pointsAwarded.ToString(CultureInfo.InvariantCulture)
                            });
                        await ServiceBackbone.SendChatMessage(winMsg);
                    }
                }

                if (placement <= 3)
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

            bool isRaceRunning;
            lock (_racerLock)
            {
                _activeRaceDropRecords[record.DropToken] = record;
                isRaceRunning = _isRaceRunning;
            }

            var placement = record.RacePlacement ?? 1;
            var lapTime = record.LapTimeSeconds ?? 0.0;
            var wreckCount = record.WreckCount ?? 0;
            var isTimeTrial = string.Equals(record.RacingMode, "TimeTrial", StringComparison.OrdinalIgnoreCase) && !isRaceRunning;

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

                bool raceFinished = false;
                List<RacerFinalResult> resultsSnapshot = [];
                lock (_racerLock)
                {
                    _singleRaceResults.Add(new RacerFinalResult(
                        record.Username,
                        record.DisplayName,
                        placement,
                        lapTime,
                        record.KnockoutCount ?? 0,
                        record.IsTotaled
                    ));
                    _singleRaceRemainingTokens.Remove(record.DropToken);
                    if (_singleRaceRemainingTokens.Count == 0 && !_singleRaceAnnounced)
                    {
                        _singleRaceAnnounced = true;
                        raceFinished = true;
                        resultsSnapshot = [.. _singleRaceResults.OrderBy(r => r.Placement).ThenBy(r => r.LapTimeSeconds)];
                        _isRaceRunning = false;
                        _heatWatchdogCts?.Cancel();
                        dropRegistry.SetSessionActive(DropGameType.Racing, false);
                    }
                }

                if (raceFinished)
                {
                    await AnnounceRaceResultsAsync(resultsSnapshot, isTournamentFinal: false, rSettings);
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
                            try
                            {
                                await Task.Delay(6000);
                                List<PendingDropRecord> nextHeat;
                                int nextNum;
                                int advancePerHeat;
                                lock (_racerLock)
                                {
                                    if (!_isRaceRunning || _activeTournament != tournament) return;
                                    nextHeat = tournament.Heats[tournament.CurrentHeatIndex];
                                    nextNum = tournament.CurrentHeatIndex + 1;
                                    advancePerHeat = tournament.AdvancePerHeat;
                                }
                                var nextHeatTitle = _currentSessionGameMode == RacingGameMode.DestructionDerby
                                    ? $"Demolition Derby Heat {nextNum} of {totalHeats} (Top {advancePerHeat} Survivors Advance)"
                                    : (_currentSessionGameMode == RacingGameMode.Elimination
                                        ? $"Elimination Heat {nextNum} of {totalHeats} (Top {advancePerHeat} Advance)"
                                        : $"Heat {nextNum} of {totalHeats} (Top {advancePerHeat} Advance)");
                                await LaunchHeatAsync(nextHeat, nextHeatTitle, nextNum, totalHeats, isFinal: false);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Failed to launch next tournament heat");
                                await StopSessionAsync("HeatTimeout");
                            }
                        });
                    }
                    else
                    {
                        // Launch Championship Final after 6-second intermission
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(6000);
                                List<PendingDropRecord> finalRacers = [];
                                lock (_racerLock)
                                {
                                    if (!_isRaceRunning || _activeTournament != tournament) return;
                                    tournament.IsChampionshipFinal = true;
                                    foreach (var finalist in tournament.QualifiedFinalists)
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
                                            RacingMode = _currentSessionGameMode.ToString(),
                                            CreatedAtUtc = DateTime.UtcNow,
                                            EarliestCompletionUtc = DateTime.UtcNow,
                                            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
                                        };
                                        dropRegistry.RegisterDrop(newRecord);
                                        finalRacers.Add(newRecord);
                                    }
                                    tournament.FinalFinishedTokens = [.. finalRacers.Select(f => f.DropToken)];
                                }

                                var finalTitle = _currentSessionGameMode == RacingGameMode.DestructionDerby
                                    ? "🏆 Demolition Championship Final"
                                    : (_currentSessionGameMode == RacingGameMode.Elimination
                                        ? "🏆 Elimination Championship Final"
                                        : "🏆 Championship Final");
                                await LaunchHeatAsync(finalRacers, finalTitle, totalHeats + 1, totalHeats, isFinal: true);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Failed to launch championship final");
                                await StopSessionAsync("HeatTimeout");
                            }
                        });
                    }
                }
            }
            else
            {
                // Championship Final completion
                await AwardRacePlacementPointsAndTriggers(record, placement, lapTime, wreckCount, rSettings);

                bool finalFinished = false;
                List<RacerFinalResult> resultsSnapshot = [];
                lock (_racerLock)
                {
                    tournament.FinalResults.Add(new RacerFinalResult(
                        record.Username,
                        record.DisplayName,
                        placement,
                        lapTime,
                        record.KnockoutCount ?? 0,
                        record.IsTotaled
                    ));
                    tournament.FinalFinishedTokens.Remove(record.DropToken);
                    if (tournament.FinalFinishedTokens.Count == 0 && !tournament.FinalAnnounced)
                    {
                        tournament.FinalAnnounced = true;
                        finalFinished = true;
                        resultsSnapshot = [.. tournament.FinalResults.OrderBy(r => r.Placement).ThenBy(r => r.LapTimeSeconds)];
                        _isRaceRunning = false;
                        _activeTournament = null;
                        _heatWatchdogCts?.Cancel();
                        dropRegistry.SetSessionActive(DropGameType.Racing, false);
                    }
                }

                if (finalFinished)
                {
                    await AnnounceRaceResultsAsync(resultsSnapshot, isTournamentFinal: true, rSettings);
                }
            }
        }

        private async Task AnnounceRaceResultsAsync(
            IReadOnlyList<RacerFinalResult> results,
            bool isTournamentFinal,
            RacingGameSettings rSettings)
        {
            if (!rSettings.AnnounceSessionStatusInChat || results.Count == 0) return;

            var isDerby = _currentSessionGameMode == RacingGameMode.DestructionDerby;
            var template = isDerby
                ? rSettings.Messages.DerbyFinalComplete
                : (isTournamentFinal ? rSettings.Messages.ChampionshipFinalComplete : rSettings.Messages.RaceComplete);
            var fallback = isDerby
                ? RacingMessageSettings.DefaultDerbyFinalComplete
                : (isTournamentFinal ? RacingMessageSettings.DefaultChampionshipFinalComplete : RacingMessageSettings.DefaultRaceComplete);

            var first = results.Count > 0 ? results[0] : null;
            var second = results.Count > 1 ? results[1] : null;
            var third = results.Count > 2 ? results[2] : null;

            var entries = new List<string>();
            var countToShow = Math.Min(results.Count, 3);
            for (var i = 0; i < countToShow; i++)
            {
                var r = results[i];
                var medal = r.Placement switch
                {
                    1 => "🥇 1st",
                    2 => "🥈 2nd",
                    3 => "🥉 3rd",
                    _ => $"{r.Placement}th"
                };

                if (isDerby)
                {
                    var koText = r.KnockoutCount == 1 ? "1 KO" : $"{r.KnockoutCount} KOs";
                    entries.Add($"{medal}: {r.DisplayName} ({koText})");
                }
                else
                {
                    entries.Add($"{medal}: {r.DisplayName} ({r.LapTimeSeconds:F2}s)");
                }
            }

            var resultsSummary = string.Join(" • ", entries);

            var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["results"] = resultsSummary,
                ["winner"] = first?.DisplayName ?? "",
                ["first"] = first?.DisplayName ?? "",
                ["second"] = second?.DisplayName ?? "",
                ["third"] = third?.DisplayName ?? "",
                ["time"] = first != null ? first.LapTimeSeconds.ToString("F2", CultureInfo.InvariantCulture) : "0.00",
                ["kills"] = first != null ? first.KnockoutCount.ToString(CultureInfo.InvariantCulture) : "0"
            };

            var msg = RacingMessageSettings.Format(template, fallback, replacements);
            await ServiceBackbone.SendChatMessage(msg);
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

                if (e.IsModOrHigher() && (firstArg.Equals("start", StringComparison.OrdinalIgnoreCase) || firstArg.Equals("derby", StringComparison.OrdinalIgnoreCase)))
                {
                    int? customDuration = null;
                    RacingGameMode? modeOverride = firstArg.Equals("derby", StringComparison.OrdinalIgnoreCase) ? RacingGameMode.DestructionDerby : null;
                    RacingTrackType? trackOverride = firstArg.Equals("derby", StringComparison.OrdinalIgnoreCase) ? RacingTrackType.DerbyBowl : null;

                    if (e.Args.Count > 1)
                    {
                        if (int.TryParse(e.Args[1], out var parsedDuration))
                        {
                            customDuration = parsedDuration;
                        }
                        else if (e.Args[1].Equals("derby", StringComparison.OrdinalIgnoreCase))
                        {
                            modeOverride = RacingGameMode.DestructionDerby;
                            trackOverride = RacingTrackType.DerbyBowl;
                            if (e.Args.Count > 2 && int.TryParse(e.Args[2], out var parsedDuration2))
                            {
                                customDuration = parsedDuration2;
                            }
                        }
                    }
                    if (!rSettings.GameEnabled)
                    {
                        logger.LogInformation("Ignored !race start from {User} because Racing is disabled", e.DisplayName);
                        return;
                    }
                    await StartRaceLobbyAsync(
                        lobbyDurationSeconds: customDuration,
                        gameModeOverride: modeOverride,
                        trackTypeOverride: trackOverride,
                        triggeredBy: e.DisplayName);
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
