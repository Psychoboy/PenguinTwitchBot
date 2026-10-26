using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.Commands.Games.Racing;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Bot.Services.Chat;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Services;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Commands.Games
{
    public class RacingGameServiceTests
    {
        private readonly Mock<IServiceBackbone> _serviceBackbone = new();
        private readonly Mock<ICommandHandler> _commandHandler = new();
        private readonly Mock<IDefaultCommandTriggerService> _defaultCommandTriggerService = new();
        private readonly Mock<IServiceScopeFactory> _scopeFactory = new();
        private readonly Mock<IEmoteService> _emoteService = new();
        private readonly Mock<IWebSocketMessenger> _webSocketMessenger = new();
        private readonly Mock<IPenguinDispatcher> _dispatcher = new();
        private readonly Mock<IDropGamesSettingsService> _settingsService = new();
        private readonly Mock<IDropRegistry> _dropRegistry = new();
        private readonly Mock<IChatColorService> _chatColorService = new();
        private readonly Mock<IPointsSystem> _pointsSystem = new();

        private readonly DropGamesSettings _settings;

        public RacingGameServiceTests()
        {
            _settings = new DropGamesSettings
            {
                Racing = new RacingGameSettings
                {
                    GameEnabled = true,
                    LobbyDurationSeconds = 60,
                    TotalLaps = 2,
                    TrackType = RacingTrackType.Perimeter,
                    WinPointsFirst = 25,
                    WinPointsSecond = 18,
                    WinPointsThird = 15,
                    ParticipationPoints = 2,
                    AnnounceSessionStatusInChat = true
                },
                Themes = DropGameTheme.CreateDefaultSeasonalThemes()
            };

            _settingsService.Setup(x => x.GetSettingsAsync()).ReturnsAsync(_settings);
            _dropRegistry.Setup(x => x.GetActiveSessionTypes()).Returns([]);
            _chatColorService.Setup(x => x.GetOrAssignColor(It.IsAny<string>(), It.IsAny<string>())).Returns("#FF0055");
        }

        private RacingGameService CreateService()
        {
            return new RacingGameService(
                _serviceBackbone.Object,
                _commandHandler.Object,
                _defaultCommandTriggerService.Object,
                _scopeFactory.Object,
                _emoteService.Object,
                _webSocketMessenger.Object,
                _dispatcher.Object,
                _settingsService.Object,
                _dropRegistry.Object,
                NullLogger<RacingGameService>.Instance,
                _chatColorService.Object,
                null,
                _pointsSystem.Object);
        }

        [Fact]
        public async Task StartRaceLobbyAsync_OpensLobby_And_BroadcastsStart()
        {
            using var service = CreateService();

            var session = await service.StartRaceLobbyAsync(60, triggeredBy: "Streamer");

            Assert.NotNull(session);
            Assert.True(session.IsActive);
            Assert.Equal("racing", session.GameType);
            Assert.Equal(60, session.TotalDurationSeconds);

            _webSocketMessenger.Verify(x => x.AddToQueue(
                WsTopics.Racing,
                It.Is<string>(json => json.Contains("race_lobby_start") && json.Contains("Perimeter"))),
                Times.Once);

            _dropRegistry.Verify(x => x.SetSessionActive(DropGameType.Racing, true), Times.Once);
        }

        [Fact]
        public async Task TriggerJoinRaceAsync_RegistersRacer_And_EmitsGridUpdate()
        {
            using var service = CreateService();

            await service.StartRaceLobbyAsync(60, triggeredBy: "Streamer");

            var eventArgs = new CommandEventArgs
            {
                Name = "speedy",
                DisplayName = "SpeedyRacer",
                UserId = "12345",
                Command = "race",
                Arg = "PogChamp"
            };

            var token = await service.TriggerJoinRaceAsync(eventArgs, "PogChamp");

            Assert.False(string.IsNullOrWhiteSpace(token));
            _dropRegistry.Verify(x => x.RegisterDrop(It.Is<PendingDropRecord>(r =>
                r.DropToken == token &&
                r.DisplayName == "SpeedyRacer" &&
                r.GameType == DropGameType.Racing)),
                Times.Once);

            _webSocketMessenger.Verify(x => x.AddToQueue(
                WsTopics.Racing,
                It.Is<string>(json => json.Contains("race_grid_update") && json.Contains("SpeedyRacer"))),
                Times.Once);
        }

        [Fact]
        public async Task TriggerSoloTimeTrialAsync_DispatchesHotLap_WithoutWaitingForLobby()
        {
            using var service = CreateService();

            var eventArgs = new CommandEventArgs
            {
                Name = "ghost",
                DisplayName = "GhostDriver",
                UserId = "67890",
                Command = "timetrial",
                Arg = "LUL"
            };

            var token = await service.TriggerSoloTimeTrialAsync(eventArgs, "LUL");

            Assert.False(string.IsNullOrWhiteSpace(token));

            _dropRegistry.Verify(x => x.RegisterDrop(It.Is<PendingDropRecord>(r =>
                r.DropToken == token &&
                r.RacingMode == "TimeTrial")),
                Times.Once);

            _webSocketMessenger.Verify(x => x.AddToQueue(
                WsTopics.Racing,
                It.Is<string>(json => json.Contains("timetrial_start") && json.Contains("GhostDriver"))),
                Times.Once);
        }

        [Fact]
        public async Task ProcessCompletionAsync_ForFirstPlace_AwardsPoints_And_TriggersWinEvent()
        {
            using var service = CreateService();

            var record = new PendingDropRecord
            {
                DropToken = "race_token_1",
                GameType = DropGameType.Racing,
                Username = "champion",
                DisplayName = "ChampionDriver",
                UserId = "999",
                EventArgs = new CommandEventArgs { Command = "race", Name = "champion", DisplayName = "ChampionDriver" },
                Theme = _settings.Themes[0],
                CreatedAtUtc = DateTime.UtcNow,
                EarliestCompletionUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
                RacePlacement = 1,
                LapTimeSeconds = 22.45,
                WreckCount = 0,
                RacingMode = "GrandPrix"
            };

            await service.ProcessCompletionAsync(record);

            _pointsSystem.Verify(x => x.AddPointsByUserIdAndGame("999", "Racing", 25), Times.Once);

            _defaultCommandTriggerService.Verify(x => x.TriggerDefaultCommandEventAsync(
                "race",
                DefaultCommandEventTypes.RacingWin,
                record.EventArgs,
                It.Is<Dictionary<string, string>>(dict => dict["Placement"] == "1" && dict["PointsAwarded"] == "25")),
                Times.Once);

            _serviceBackbone.Verify(x => x.SendChatMessage(
                It.Is<string>(msg => msg.Contains("1ST PLACE") && msg.Contains("ChampionDriver"))),
                Times.Once);
        }

        [Fact]
        public async Task StopSessionAsync_ClosesSession_And_BroadcastsEnd()
        {
            using var service = CreateService();

            await service.StartRaceLobbyAsync(60, triggeredBy: "Streamer");

            var result = await service.StopSessionAsync(triggeredBy: "Streamer");

            Assert.NotNull(result);
            Assert.False(result.IsActive);

            _dropRegistry.Verify(x => x.SetSessionActive(DropGameType.Racing, false), Times.Once);

            _webSocketMessenger.Verify(x => x.AddToQueue(
                WsTopics.Racing,
                It.Is<string>(json => json.Contains("race_end"))),
                Times.Once);
        }

        [Fact]
        public async Task AddTestRacersAsync_PopulatesMultipleRacers_And_EmitsGridUpdate()
        {
            using var service = CreateService();

            var added = await service.AddTestRacersAsync(8, "TestEmote", autoLaunch: false);

            Assert.Equal(8, added);
            _dropRegistry.Verify(x => x.RegisterDrop(It.IsAny<PendingDropRecord>()), Times.Exactly(8));
            _webSocketMessenger.Verify(x => x.AddToQueue(
                WsTopics.Racing,
                It.Is<string>(json => json.Contains("race_grid_update"))),
                Times.Once);
        }

        [Fact]
        public async Task MultiHeatTournament_SplitsRacers_And_LaunchesHeat1()
        {
            _settings.Racing.MaxRacersPerHeat = 6;
            using var service = CreateService();

            // Add 14 racers (exceeds max 6, so splits into multiple heats)
            await service.AddTestRacersAsync(14, "TestEmote", autoLaunch: true);

            // Verify Heat 1 was dispatched with heat title
            _webSocketMessenger.Verify(x => x.AddToQueue(
                WsTopics.Racing,
                It.Is<string>(json => json.Contains("race_start") && json.Contains("Heat 1 of"))),
                Times.Once);
        }

        [Fact]
        public void GeneratedRacingSounds_ExistAndPassAudioValidation()
        {
            var files = new[]
            {
                "racing_start.wav",
                "racing_end.wav",
                "racing_engine.wav",
                "racing_screech.wav",
                "racing_crash.wav",
                "racing_boost.wav",
                "racing_win.wav"
            };

            var baseDir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(baseDir) && !Directory.Exists(Path.Combine(baseDir, "PenguinTwitchBot", "wwwroot", "media")))
            {
                var parent = Directory.GetParent(baseDir);
                if (parent == null) break;
                baseDir = parent.FullName;
            }

            var mediaDir = Path.Combine(baseDir, "PenguinTwitchBot", "wwwroot", "media");
            Assert.True(Directory.Exists(mediaDir), $"Media directory not found at {mediaDir}");

            foreach (var file in files)
            {
                var path = Path.Combine(mediaDir, file);
                Assert.True(File.Exists(path), $"Missing generated racing sound: {file}");
                var bytes = File.ReadAllBytes(path);
                Assert.True(bytes.Length > 1000, $"File {file} too small: {bytes.Length} bytes");
                Assert.True(MediaUploadService.ValidateAudioSignature(bytes, ".wav"), $"Invalid WAV signature for {file}");
            }
        }

        [Fact]
        public void RacingSettings_DefaultSounds_AreConfiguredToRacingWavFiles()
        {
            var rSettings = new RacingGameSettings();

            Assert.Equal("/media/racing_start.wav", rSettings.StartSound.AudioFile);
            Assert.Equal("/media/racing_end.wav", rSettings.EndSound.AudioFile);
            Assert.Equal("/media/racing_engine.wav", rSettings.EngineSound.AudioFile);
            Assert.Equal("/media/racing_screech.wav", rSettings.ScreechSound.AudioFile);
            Assert.Equal("/media/racing_crash.wav", rSettings.CrashSound.AudioFile);
            Assert.Equal("/media/racing_boost.wav", rSettings.BoostSound.AudioFile);
            Assert.Equal("/media/racing_win.wav", rSettings.WinSound.AudioFile);

            var webRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PenguinTwitchBot", "wwwroot"));
            var mediaService = new MediaUploadService(webRoot, NullLogger<MediaUploadService>.Instance);

            var theme = new DropGameTheme();
            var bundle = DropGameMediaResolver.ResolveRacingSoundBundle(rSettings, theme, mediaService);

            Assert.NotNull(bundle.RacingStart);
            Assert.Equal("/media/racing_start.wav", bundle.RacingStart.Url);
            Assert.NotNull(bundle.RacingEnd);
            Assert.Equal("/media/racing_end.wav", bundle.RacingEnd.Url);
            Assert.NotNull(bundle.RacingEngine);
            Assert.Equal("/media/racing_engine.wav", bundle.RacingEngine.Url);
            Assert.NotNull(bundle.RacingScreech);
            Assert.Equal("/media/racing_screech.wav", bundle.RacingScreech.Url);
            Assert.NotNull(bundle.RacingCrash);
            Assert.Equal("/media/racing_crash.wav", bundle.RacingCrash.Url);
            Assert.NotNull(bundle.RacingBoost);
            Assert.Equal("/media/racing_boost.wav", bundle.RacingBoost.Url);
            Assert.NotNull(bundle.RacingWin);
            Assert.Equal("/media/racing_win.wav", bundle.RacingWin.Url);
        }

        [Fact]
        public void RacingMessageSettings_Format_ReplacesAllPlaceholders()
        {
            var tpl = "Welcome {user}! Heat {heat} has {count} racers. Winner gets {points}pts in {time}s ({wrecks} wrecks).";
            var replacements = new Dictionary<string, string>
            {
                ["user"] = "SpeedyPenguin",
                ["heat"] = "Final",
                ["count"] = "8",
                ["points"] = "25",
                ["time"] = "14.20",
                ["wrecks"] = "1"
            };

            var formatted = RacingMessageSettings.Format(tpl, "Fallback", replacements);
            Assert.Equal("Welcome SpeedyPenguin! Heat Final has 8 racers. Winner gets 25pts in 14.20s (1 wrecks).", formatted);
        }

        [Fact]
        public async Task RacingGameService_DispatchesCustomizedChatMessages()
        {
            _settings.Racing.Messages.SessionStart = "Custom Start: {duration}s lobby for {mode}!";
            _settings.Racing.Messages.ChampionshipFinalStart = "Custom Green Flag: {count} cars!";
            _settings.Racing.Messages.RaceWin = "Custom Winner: {user} won +{points} in {time}s!";

            using var service = CreateService();

            // 1. Lobby message check
            await service.StartRaceLobbyAsync(45, null, "Tester");
            _serviceBackbone.Verify(x => x.SendChatMessage(
                It.Is<string>(msg => msg == "Custom Start: 45s lobby for GrandPrix!")),
                Times.Once);

            // 2. Race start green flag check
            await service.AddTestRacersAsync(4, "TestEmote", autoLaunch: true);
            _serviceBackbone.Verify(x => x.SendChatMessage(
                It.Is<string>(msg => msg == "Custom Green Flag: 4 cars!")),
                Times.Once);

            // 3. Race win check
            var winnerRecord = new PendingDropRecord
            {
                DropToken = Guid.NewGuid().ToString("N"),
                GameType = DropGameType.Racing,
                Username = "TestDriver",
                DisplayName = "TestDriver",
                UserId = "user-1",
                RacePlacement = 1,
                LapTimeSeconds = 12.34,
                Score = 25,
                EventArgs = new CommandEventArgs { Command = "race", DisplayName = "TestDriver", Name = "TestDriver", UserId = "user-1" },
                Theme = new DropGameTheme { Name = "Spring" },
                CreatedAtUtc = DateTime.UtcNow,
                EarliestCompletionUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
            };

            await service.ProcessCompletionAsync(winnerRecord);
            _serviceBackbone.Verify(x => x.SendChatMessage(
                It.Is<string>(msg => msg == "Custom Winner: TestDriver won +25 in 12.34s!")),
                Times.Once);
        }

        [Fact]
        public async Task TriggerSoloTimeTrialAsync_WhenLobbyOrRaceActive_ReturnsEmptyAndSendsRaceInProgress()
        {
            using var service = CreateService();

            // Open a lobby so session is active
            await service.StartRaceLobbyAsync(60, null, "Host");

            var args = new CommandEventArgs
            {
                Command = "timetrial",
                Name = "LateRacer",
                DisplayName = "LateRacer",
                UserId = "user-late"
            };

            var token = await service.TriggerSoloTimeTrialAsync(args);

            Assert.Equal(string.Empty, token);
            _serviceBackbone.Verify(x => x.SendChatMessage(
                It.Is<string>(m => m.Contains("LateRacer") && m.Contains("race is currently in progress"))),
                Times.Once);
            _dropRegistry.Verify(x => x.RegisterDrop(It.IsAny<PendingDropRecord>()), Times.Never);
        }

        [Fact]
        public async Task TriggerJoinRaceAsync_EnforcesMaxLobbyRacers_AndDuplicateUser()
        {
            _settings.Racing.MaxLobbyRacers = 2;
            using var service = CreateService();

            await service.StartRaceLobbyAsync(60, null, "Host");

            var racer1 = new CommandEventArgs { Command = "race", Name = "racer1", DisplayName = "Racer 1", UserId = "user-1" };
            var racer2 = new CommandEventArgs { Command = "race", Name = "racer2", DisplayName = "Racer 2", UserId = "user-2" };
            var racer3 = new CommandEventArgs { Command = "race", Name = "racer3", DisplayName = "Racer 3", UserId = "user-3" };

            var token1 = await service.TriggerJoinRaceAsync(racer1);
            Assert.False(string.IsNullOrEmpty(token1));

            // Duplicate racer1
            var dupToken = await service.TriggerJoinRaceAsync(racer1);
            Assert.Equal(string.Empty, dupToken);

            var token2 = await service.TriggerJoinRaceAsync(racer2);
            Assert.False(string.IsNullOrEmpty(token2));

            // Grid full (MaxLobbyRacers = 2)
            var token3 = await service.TriggerJoinRaceAsync(racer3);
            Assert.Equal(string.Empty, token3);
            _serviceBackbone.Verify(x => x.SendChatMessage(
                It.Is<string>(m => m.Contains("Racer 3") && m.Contains("grid is full"))),
                Times.Once);
        }

        [Fact]
        public async Task AddTestRacersAsync_WhenGameDisabled_ReturnsZeroWithoutEnabling()
        {
            _settings.Racing.GameEnabled = false;
            using var service = CreateService();

            var added = await service.AddTestRacersAsync(5);

            Assert.Equal(0, added);
            Assert.False(_settings.Racing.GameEnabled);
            _settingsService.Verify(x => x.SaveSettingsAsync(It.IsAny<DropGamesSettings>()), Times.Never);
        }
    }
}

