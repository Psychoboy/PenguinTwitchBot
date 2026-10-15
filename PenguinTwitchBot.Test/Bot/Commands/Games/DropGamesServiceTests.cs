using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PenguinTwitchBot.Application.DropGameNotifications;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Bot.Commands.Games.Parachute;
using PenguinTwitchBot.Bot.Commands.Games.Plinko;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Models.Commands;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Database.Bot.Models.Overlay;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Services;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Commands.Games
{
    public class DropGamesServiceTests
    {
        private readonly Mock<IServiceBackbone> _serviceBackbone = new();
        private readonly Mock<ICommandHandler> _commandHandler = new();
        private readonly Mock<IDefaultCommandTriggerService> _defaultCommandTriggerService = new();
        private readonly Mock<IServiceScopeFactory> _scopeFactory = new();
        private readonly Mock<IServiceScope> _serviceScope = new();
        private readonly Mock<IServiceProvider> _serviceProvider = new();
        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<ISettingsRepository> _settingsRepo = new();
        private readonly Mock<IActionManagementService> _actionManagementService = new();
        private readonly Mock<IAction> _actionService = new();
        private readonly Mock<IEmoteService> _emoteService = new();
        private readonly Mock<IWebSocketMessenger> _webSocketMessenger = new();
        private readonly Mock<IPenguinDispatcher> _dispatcher = new();

        public DropGamesServiceTests()
        {
            _scopeFactory.Setup(x => x.CreateScope()).Returns(_serviceScope.Object);
            _serviceScope.Setup(x => x.ServiceProvider).Returns(_serviceProvider.Object);
            _serviceProvider.Setup(x => x.GetService(typeof(IUnitOfWork))).Returns(_unitOfWork.Object);
            _serviceProvider.Setup(x => x.GetService(typeof(IActionManagementService))).Returns(_actionManagementService.Object);
            _serviceProvider.Setup(x => x.GetService(typeof(IAction))).Returns(_actionService.Object);
            _unitOfWork.Setup(x => x.Settings).Returns(_settingsRepo.Object);
            _settingsRepo.Setup(x => x.GetAsync(
                It.IsAny<Expression<Func<Setting, bool>>>(),
                It.IsAny<Func<IQueryable<Setting>, IOrderedQueryable<Setting>>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string>()))
                .ReturnsAsync([]);
        }

        private IDropGamesSettingsService CreateSettingsService()
        {
            return new DropGamesSettingsService(_scopeFactory.Object, NullLogger<DropGamesSettingsService>.Instance);
        }

        private IDropRegistry CreateDropRegistry()
        {
            return new DropRegistry(NullLogger<DropRegistry>.Instance);
        }

        private ParachuteDropService CreateParachuteService(
            IDropGamesSettingsService? settingsService = null,
            IDropRegistry? dropRegistry = null)
        {
            return new ParachuteDropService(
                _serviceBackbone.Object,
                _commandHandler.Object,
                _defaultCommandTriggerService.Object,
                _scopeFactory.Object,
                _emoteService.Object,
                _webSocketMessenger.Object,
                _dispatcher.Object,
                settingsService ?? CreateSettingsService(),
                dropRegistry ?? CreateDropRegistry(),
                NullLogger<ParachuteDropService>.Instance);
        }

        private PlinkoDropService CreatePlinkoService(
            IDropGamesSettingsService? settingsService = null,
            IDropRegistry? dropRegistry = null)
        {
            return new PlinkoDropService(
                _serviceBackbone.Object,
                _commandHandler.Object,
                _defaultCommandTriggerService.Object,
                _scopeFactory.Object,
                _emoteService.Object,
                _webSocketMessenger.Object,
                _dispatcher.Object,
                settingsService ?? CreateSettingsService(),
                dropRegistry ?? CreateDropRegistry(),
                NullLogger<PlinkoDropService>.Instance);
        }

        [Fact]
        public async Task GetSettingsAsync_InitializesFourStockSeasonalThemesAndDefaultPlinkoBuckets()
        {
            var settingsService = CreateSettingsService();

            var settings = await settingsService.GetSettingsAsync();

            Assert.NotNull(settings);
            Assert.Contains(settings.Themes, t => t.Id == "spring" && t.SeasonPreset == "spring" && t.IsStock);
            Assert.Contains(settings.Themes, t => t.Id == "summer" && t.SeasonPreset == "summer" && t.IsStock);
            Assert.Contains(settings.Themes, t => t.Id == "fall" && t.SeasonPreset == "fall" && t.IsStock);
            Assert.Contains(settings.Themes, t => t.Id == "winter" && t.SeasonPreset == "winter" && t.IsStock);
            Assert.NotEmpty(settings.Plinko.Buckets);
            Assert.Equal(15, settings.Plinko.PegTransparency);
        }

        [Fact]
        public void PlinkoGameSettings_PegTransparency_DefaultsTo15_AndClampsProperly()
        {
            var settings = new PlinkoGameSettings();
            Assert.Equal(15, settings.PegTransparency);

            settings.PegTransparency = 100;
            Assert.Equal(100, settings.PegTransparency);

            settings.PegTransparency = 0;
            Assert.Equal(0, settings.PegTransparency);

            settings.PegTransparency = -10;
            Assert.Equal(0, settings.PegTransparency);

            settings.PegTransparency = 150;
            Assert.Equal(100, settings.PegTransparency);
        }

        [Fact]
        public async Task TriggerPlinkoDropAsync_IncludesPegTransparencyInBoardPayload()
        {
            var registry = CreateDropRegistry();
            var settingsService = CreateSettingsService();
            var settings = await settingsService.GetSettingsAsync();
            settings.Plinko.PegTransparency = 35;
            await settingsService.SaveSettingsAsync(settings);

            string? capturedTopic = null;
            string? capturedJson = null;
            _webSocketMessenger
                .Setup(m => m.AddToQueue(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((topic, json) =>
                {
                    capturedTopic = topic;
                    capturedJson = json;
                })
                .Returns(Task.CompletedTask);

            var sut = CreatePlinkoService(settingsService, registry);
            var token = await sut.TriggerPlinkoDropAsync(new CommandEventArgs { Name = "viewer1", DisplayName = "Viewer1" });

            Assert.NotNull(capturedJson);
            Assert.Equal(WsTopics.Plinko, capturedTopic);
            using var doc = System.Text.Json.JsonDocument.Parse(capturedJson);
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("board", out var board));
            Assert.True(board.TryGetProperty("pegTransparency", out var pegTransparency));
            Assert.Equal(35, pegTransparency.GetInt32());
        }

        [Fact]
        public async Task ResolveCharacterOrEmoteUrlAsync_PrioritizesNativeTwitchEmote_ThenEmoteService_ThenFallback()
        {
            _emoteService.Setup(x => x.GetEmotesAsync()).ReturnsAsync(new Dictionary<string, string>
            {
                ["PenguinDance"] = "https://cdn.7tv.app/emote/penguin_dance/3x.webp"
            });

            // 1. Native Twitch emote fragment URL takes priority
            var nativeEventArgs = new CommandEventArgs
            {
                Name = "viewer1",
                DisplayName = "Viewer1",
                Arg = "PenguinDance",
                EmoteUrl = "https://static-cdn.jtvnw.net/emoticons/v2/12345/default/dark/3.0"
            };
            var resolvedNative = await DropGameMediaResolver.ResolveCharacterOrEmoteUrlAsync(_emoteService.Object, nativeEventArgs, null, "/fallback.png");
            Assert.Equal("https://static-cdn.jtvnw.net/emoticons/v2/12345/default/dark/3.0", resolvedNative);

            // 2. BTTV/FFZ/7TV/Channel emote code resolved via IEmoteService
            var bttvEventArgs = new CommandEventArgs
            {
                Name = "viewer1",
                DisplayName = "Viewer1",
                Arg = "penguindance extra text"
            };
            var resolvedEmote = await DropGameMediaResolver.ResolveCharacterOrEmoteUrlAsync(_emoteService.Object, bttvEventArgs, null, "/fallback.png");
            Assert.Equal("https://cdn.7tv.app/emote/penguin_dance/3x.webp", resolvedEmote);

            // 3. Direct custom URL is preserved when allowDirectUrlOrPath is true
            var resolvedDirect = await DropGameMediaResolver.ResolveCharacterOrEmoteUrlAsync(_emoteService.Object, null, "/media/custom_chibi.png", "/fallback.png", allowDirectUrlOrPath: true);
            Assert.Equal("/media/custom_chibi.png", resolvedDirect);

            // Chat-sourced direct path without explicit opt-in falls back safely
            var resolvedUntrustedPath = await DropGameMediaResolver.ResolveCharacterOrEmoteUrlAsync(_emoteService.Object, null, "/media/custom_chibi.png", "/fallback.png", allowDirectUrlOrPath: false);
            Assert.Equal("/fallback.png", resolvedUntrustedPath);

            // 4. Unknown word or empty input falls back to theme default character sprite
            var noEmoteArgs = new CommandEventArgs
            {
                Name = "viewer1",
                DisplayName = "Viewer1",
                Arg = "NotAnEmote"
            };
            var resolvedFallback = await DropGameMediaResolver.ResolveCharacterOrEmoteUrlAsync(_emoteService.Object, noEmoteArgs, null, "/themes/spring_default.png");
            Assert.Equal("/themes/spring_default.png", resolvedFallback);
        }

        [Fact]
        public async Task TriggerParachuteDropAsync_CreatesAuthoritativeDropAndQueuesOnParachuteTopic()
        {
            var registry = CreateDropRegistry();
            var sut = CreateParachuteService(dropRegistry: registry);

            var token = await sut.TriggerParachuteDropAsync(
                new CommandEventArgs { Name = "testuser", DisplayName = "TestUser", UserId = "1001" },
                emoteOrImage: null,
                themeOverrideId: "winter",
                forcedStartX: 0.50,
                forcedWindAccelX: 0.0);

            Assert.False(string.IsNullOrWhiteSpace(token));
            Assert.Equal(64, token.Length); // 256-bit hex token

            var pending = registry.GetPendingDrop(token);
            Assert.NotNull(pending);
            Assert.Equal(DropGameType.Parachute, pending.GameType);
            Assert.Equal("winter", pending.Theme.Id);
            Assert.True(pending.IsBullseye);
            Assert.True(pending.IsWin);
            Assert.Equal(100, pending.Score);

            _webSocketMessenger.Verify(
                x => x.AddToQueue(
                    WsTopics.Parachute,
                    It.Is<string>(json => json.Contains(token) && json.Contains("parachute_drop"))),
                Times.Once);
        }

        [Fact]
        public async Task SpawnDropletsAsync_TargetedGiantHyperDroplets_CollideAndAlterParachutistTrajectory()
        {
            var registry = CreateDropRegistry();
            var sut = CreateParachuteService(dropRegistry: registry);

            // Start a parachute drop aimed dead center (bullseye)
            var token = await sut.TriggerParachuteDropAsync(
                new CommandEventArgs { Name = "parachuter", DisplayName = "Parachuter", UserId = "2002" },
                emoteOrImage: null,
                themeOverrideId: "spring",
                forcedStartX: 0.50,
                forcedWindAccelX: 0.0);

            var before = registry.GetPendingDrop(token)!;
            var initialLandingX = before.LandingX;
            Assert.Equal(0, before.DropletHitCount);

            // Fire a targeted barrage of Giant Hyper droplets to knock the character off-course
            var spawned = await sut.SpawnDropletsAsync(new DropletSpawnRequest
            {
                Count = 6,
                SizePreset = "Giant",
                SpeedPreset = "Hyper",
                SpawnPattern = "Targeted",
                KnockbackMultiplier = 2.0,
                TriggeredByUser = "ChaosViewer"
            });

            Assert.Equal(6, spawned.Count);

            var after = registry.GetPendingDrop(token)!;
            Assert.True(after.DropletHitCount > 0, "Expected targeted droplets to collide with the active parachutist.");
            Assert.NotEmpty(after.Collisions);
            Assert.NotEqual(initialLandingX, after.LandingX);

            // Verify parachute_droplets message with droplets + trajectoryUpdates was queued on WsTopics.Parachute
            _webSocketMessenger.Verify(
                x => x.AddToQueue(
                    WsTopics.Parachute,
                    It.Is<string>(json =>
                        json.Contains("parachute_droplets") &&
                        json.Contains("trajectoryUpdates") &&
                        json.Contains(token))),
                Times.Once);
        }

        [Fact]
        public void DropletPatternGenerator_ResolveDropletSize_RespectsAllowedRandomSizesPool()
        {
            // 1. Single allowed size in pool
            for (int i = 0; i < 20; i++)
            {
                var (label, scale, radius, mass) = DropletPatternGenerator.ResolveDropletSize("Random", ["Giant"]);
                Assert.Equal("Giant", label);
                Assert.Equal(2.0, scale);
            }

            // 2. Specific subset pool: Small and Medium only
            var allowedSmallMedium = new List<string> { "Small", "Medium" };
            for (int i = 0; i < 50; i++)
            {
                var (label, _, _, _) = DropletPatternGenerator.ResolveDropletSize("Random", allowedSmallMedium);
                Assert.True(label == "Small" || label == "Medium", $"Expected Small or Medium, got {label}");
            }

            // 3. String preset with colon notation: "Random:Large,Giant"
            for (int i = 0; i < 50; i++)
            {
                var (label, _, _, _) = DropletPatternGenerator.ResolveDropletSize("Random:Large,Giant");
                Assert.True(label == "Large" || label == "Giant", $"Expected Large or Giant, got {label}");
            }

            // 4. Fallback to all sizes when pool is empty or null
            var allObserved = new HashSet<string>();
            for (int i = 0; i < 100; i++)
            {
                var (label, _, _, _) = DropletPatternGenerator.ResolveDropletSize("Random", []);
                allObserved.Add(label);
            }
            Assert.Contains("Small", allObserved);
            Assert.Contains("Medium", allObserved);
        }

        [Fact]
        public async Task SpawnDropletsAsync_WithAllowedRandomSizes_SpawnsOnlyAllowedSizes()
        {
            var registry = CreateDropRegistry();
            var sut = CreateParachuteService(dropRegistry: registry);

            var spawned = await sut.SpawnDropletsAsync(new DropletSpawnRequest
            {
                Count = 15,
                SizePreset = "Random",
                AllowedRandomSizes = ["Small", "Large"],
                SpeedPreset = "Normal",
                SpawnPattern = "TopRain"
            });

            Assert.Equal(15, spawned.Count);
            foreach (var droplet in spawned)
            {
                Assert.True(droplet.SizeLabel == "Small" || droplet.SizeLabel == "Large",
                    $"Expected droplet size to be Small or Large, but was {droplet.SizeLabel}");
                Assert.True(droplet.SizeScale == 0.60 || droplet.SizeScale == 1.50);
            }
        }

        [Fact]
        public async Task CompleteDropAsync_EnforcesTamperProofTokenSecurity_SingleUse_AndTriggersWinActions()
        {
            var settingsService = CreateSettingsService();
            var registry = CreateDropRegistry();
            var sut = CreateParachuteService(settingsService, registry);

            var settings = await settingsService.GetSettingsAsync();
            settings.Parachute.WinActionName = "ParachuteWinnerReward";
            settings.Parachute.AnnounceWinsInChat = false;
            await settingsService.SaveSettingsAsync(settings);

            var rewardAction = new ActionType { Name = "ParachuteWinnerReward", Enabled = true };
            _actionManagementService.Setup(x => x.GetAllActionsAsync()).ReturnsAsync([rewardAction]);

            // 1. Unknown/forged token is rejected
            var forgedResult = registry.TryConsumeDrop("0000000000000000000000000000000000000000000000000000000000000000", out _, out _);
            Assert.False(forgedResult);
            _actionService.Verify(x => x.EnqueueAction(It.IsAny<ConcurrentDictionary<string, string>>(), It.IsAny<ActionType>()), Times.Never);

            // 2. Premature completion before EarliestCompletionUtc is rejected
            var prematureToken = await sut.TriggerParachuteDropAsync(
                new CommandEventArgs { Name = "earlyuser", DisplayName = "EarlyUser", UserId = "3002" },
                forcedStartX: 0.50,
                forcedWindAccelX: 0.0,
                skipMinimumDurationCheck: false);

            var prematureResult = registry.TryConsumeDrop(prematureToken, out _, out _);
            Assert.False(prematureResult);
            _actionService.Verify(x => x.EnqueueAction(It.IsAny<ConcurrentDictionary<string, string>>(), It.IsAny<ActionType>()), Times.Never);

            // 3. Valid completion when flight time has elapsed
            var validToken = await sut.TriggerParachuteDropAsync(
                new CommandEventArgs { Name = "winner", DisplayName = "Winner", UserId = "3003" },
                emoteOrImage: null,
                themeOverrideId: "summer",
                forcedStartX: 0.50,
                forcedWindAccelX: 0.0,
                skipMinimumDurationCheck: true);

            var validResult = registry.TryConsumeDrop(validToken, out var validRecord, out _);
            Assert.True(validResult);
            Assert.NotNull(validRecord);

            await sut.ProcessCompletionAsync(validRecord);

            // Verify Default Command Trigger and Direct Win Action were both invoked
            _defaultCommandTriggerService.Verify(
                x => x.TriggerDefaultCommandEventAsync(
                    "drop",
                    DefaultCommandEventTypes.ParachuteBullseye,
                    It.Is<CommandEventArgs>(e => e.Name == "winner"),
                    It.Is<Dictionary<string, string>>(v => v["Score"] == "100" && v["IsBullseye"] == "true")),
                Times.Once);

            _actionService.Verify(
                x => x.EnqueueAction(
                    It.Is<ConcurrentDictionary<string, string>>(v =>
                        v["User"] == "Winner" &&
                        v["Score"] == "100" &&
                        v["IsBullseye"] == "true"),
                    rewardAction),
                Times.Once);

            // 4. Replaying the same token a second time is rejected (atomic single-use)
            var replayResult = registry.TryConsumeDrop(validToken, out _, out _);
            Assert.False(replayResult);
            _actionService.Verify(x => x.EnqueueAction(It.IsAny<ConcurrentDictionary<string, string>>(), It.IsAny<ActionType>()), Times.Once);
        }

        [Fact]
        public async Task TriggerPlinkoDropAsync_SelectsWinningBucket_AndCompletesSecurely()
        {
            var settingsService = CreateSettingsService();
            var registry = CreateDropRegistry();
            var sut = CreatePlinkoService(settingsService, registry);

            var settings = await settingsService.GetSettingsAsync();
            settings.Plinko.WinActionName = "PlinkoJackpotAction";
            settings.Plinko.AnnounceWinsInChat = false;
            await settingsService.SaveSettingsAsync(settings);

            var plinkoAction = new ActionType { Name = "PlinkoJackpotAction", Enabled = true };
            _actionManagementService.Setup(x => x.GetAllActionsAsync()).ReturnsAsync([plinkoAction]);

            var winBucketIndex = settings.Plinko.Buckets.FindIndex(b => b.IsWin && b.Score >= 500);
            Assert.True(winBucketIndex >= 0);

            var token = await sut.TriggerPlinkoDropAsync(
                new CommandEventArgs { Name = "plinkofan", DisplayName = "PlinkoFan", UserId = "4004" },
                emoteOrImage: null,
                themeOverrideId: "fall",
                forcedBucketIndex: winBucketIndex,
                skipMinimumDurationCheck: true);

            var pending = registry.GetPendingDrop(token);
            Assert.NotNull(pending);
            Assert.Equal(DropGameType.Plinko, pending.GameType);
            Assert.True(pending.IsWin);

            var completed = registry.TryConsumeDrop(token, out var record, out _);
            Assert.True(completed);
            Assert.NotNull(record);

            await sut.ProcessCompletionAsync(record);

            _defaultCommandTriggerService.Verify(
                x => x.TriggerDefaultCommandEventAsync(
                    "plinko",
                    DefaultCommandEventTypes.PlinkoWin,
                    It.Is<CommandEventArgs>(e => e.Name == "plinkofan"),
                    It.Is<Dictionary<string, string>>(v => v.ContainsKey("BucketLabel") && v["IsWin"] == "true")),
                Times.Once);

            _actionService.Verify(
                x => x.EnqueueAction(
                    It.Is<ConcurrentDictionary<string, string>>(v => v["User"] == "PlinkoFan" && v["IsWin"] == "true"),
                    plinkoAction),
                Times.Once);
        }

        [Fact]
        public async Task TriggerPlinkoDropAsync_WhenOnlyJackpotIsWin_ReloadsFromJsonAndNonWinningBucketsDoNotWin()
        {
            Setting? savedSetting = null;
            _settingsRepo.Setup(x => x.AddAsync(It.IsAny<Setting>()))
                .Callback<Setting>(s => savedSetting = s);
            _settingsRepo.Setup(x => x.Update(It.IsAny<Setting>()))
                .Callback<Setting>(s => savedSetting = s);

            var settingsService1 = CreateSettingsService();
            var settings = await settingsService1.GetSettingsAsync();
            foreach (var b in settings.Plinko.Buckets)
            {
                b.IsWin = b.Label == "JACKPOT";
            }
            settings.Plinko.WinActionName = "PlinkoJackpotAction";
            settings.Plinko.AnnounceWinsInChat = true;
            await settingsService1.SaveSettingsAsync(settings);

            Assert.NotNull(savedSetting);

            // Simulate reloading from DB in a fresh service instance
            _settingsRepo.Setup(x => x.GetAsync(
                It.IsAny<Expression<Func<Setting, bool>>>(),
                It.IsAny<Func<IQueryable<Setting>, IOrderedQueryable<Setting>>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string>()))
                .ReturnsAsync(() => savedSetting != null ? [savedSetting] : []);

            var settingsService2 = CreateSettingsService();
            var registry = CreateDropRegistry();
            var sut2 = CreatePlinkoService(settingsService2, registry);

            var reloaded = await settingsService2.GetSettingsAsync();
            Assert.Equal(7, reloaded.Plinko.Buckets.Count);
            var singleWin = Assert.Single(reloaded.Plinko.Buckets, b => b.IsWin);
            Assert.Equal("JACKPOT", singleWin.Label);

            // Force landing in index 1 ("50 PTS", where IsWin == false)
            var token = await sut2.TriggerPlinkoDropAsync(
                new CommandEventArgs { Name = "viewer2", DisplayName = "Viewer2", UserId = "5005" },
                forcedBucketIndex: 1,
                skipMinimumDurationCheck: true);

            var pending = registry.GetPendingDrop(token);
            Assert.NotNull(pending);
            Assert.False(pending.IsWin);
            Assert.Equal(0, pending.Score);

            var completed = registry.TryConsumeDrop(token, out var record, out _);
            Assert.True(completed);
            Assert.NotNull(record);

            await sut2.ProcessCompletionAsync(record);

            _defaultCommandTriggerService.Verify(
                x => x.TriggerDefaultCommandEventAsync(
                    "plinko",
                    DefaultCommandEventTypes.PlinkoLose,
                    It.Is<CommandEventArgs>(e => e.Name == "viewer2"),
                    It.Is<Dictionary<string, string>>(v => v["IsWin"] == "false" && v["Score"] == "0")),
                Times.Once);
            _actionService.Verify(x => x.EnqueueAction(It.IsAny<ConcurrentDictionary<string, string>>(), It.IsAny<ActionType>()), Times.Never);
            _serviceBackbone.Verify(x => x.SendChatMessage(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void SubActionRegistry_And_WidgetRegistry_ContainDropGamesAndDroplets()
        {
            Assert.True(SubActionRegistry.Metadata.TryGetValue(SubActionTypes.ParachuteDrop, out var dropMeta));
            Assert.Equal(typeof(ParachuteDropType), dropMeta!.Type);

            Assert.True(SubActionRegistry.Metadata.TryGetValue(SubActionTypes.ParachuteDroplets, out var dropletsMeta));
            Assert.Equal(typeof(ParachuteDropletsType), dropletsMeta!.Type);

            Assert.True(SubActionRegistry.Metadata.TryGetValue(SubActionTypes.PlinkoDrop, out var plinkoMeta));
            Assert.Equal(typeof(PlinkoDropType), plinkoMeta!.Type);

            Assert.True(SubActionRegistry.Metadata.TryGetValue(SubActionTypes.StartDropGameSession, out var startMeta));
            Assert.Equal(typeof(StartDropGameSessionType), startMeta!.Type);

            Assert.True(SubActionRegistry.Metadata.TryGetValue(SubActionTypes.StopDropGameSession, out var stopMeta));
            Assert.Equal(typeof(StopDropGameSessionType), stopMeta!.Type);

            Assert.Contains(WidgetRegistry.All, w => w.Type == "parachute" && w.SourcePath == "/parachute.html");
            Assert.Contains(WidgetRegistry.All, w => w.Type == "plinko" && w.SourcePath == "/plinko.html");
        }

        [Fact]
        public async Task StartAndStopSession_BroadcastsWebSocketEvents_AndSetsState()
        {
            var sut = CreateParachuteService();

            var startDto = await sut.StartSessionAsync(90, triggeredBy: "Streamer");
            Assert.True(startDto.IsActive);
            Assert.Equal(90, startDto.TotalDurationSeconds);
            Assert.Equal(0, startDto.TotalDrops);

            var current = sut.GetCurrentSession();
            Assert.NotNull(current);
            Assert.True(current.IsActive);

            _webSocketMessenger.Verify(x => x.AddToQueue(
                WsTopics.Parachute,
                It.Is<string>(json => json.Contains("session_start") && json.Contains("\"totalDurationSeconds\":90"))),
                Times.Once);

            var stopDto = await sut.StopSessionAsync(triggeredBy: "Streamer");
            Assert.NotNull(stopDto);
            Assert.False(stopDto.IsActive);

            var afterStop = sut.GetCurrentSession();
            Assert.Null(afterStop);

            _webSocketMessenger.Verify(x => x.AddToQueue(
                WsTopics.Parachute,
                It.Is<string>(json => json.Contains("session_end"))),
                Times.Once);
        }

        [Fact]
        public async Task OnCommand_AutoStartsSession_WhenEnabled_AndEnforcesMaxDrops()
        {
            var settingsService = CreateSettingsService();
            var sut = CreateParachuteService(settingsService);

            var settings = await settingsService.GetSettingsAsync();
            settings.Parachute.SessionModeEnabled = true;
            settings.Parachute.AutoStartOnCommand = true;
            settings.Parachute.DefaultSessionDurationSeconds = 60;
            settings.Parachute.MaxDropsPerUserPerSession = 1;
            await settingsService.SaveSettingsAsync(settings);

            _commandHandler.Setup(x => x.GetCommand("drop")).Returns(new Command(
                new BaseCommandProperties
                {
                    CommandName = "drop"
                }, sut));

            // First drop should auto-start session and trigger drop
            var e1 = new CommandEventArgs
            {
                Command = "drop",
                Name = "viewer1",
                DisplayName = "Viewer1",
                UserId = "user-1"
            };
            await sut.OnCommand(this, e1);

            var session = sut.GetCurrentSession();
            Assert.NotNull(session);
            Assert.True(session.IsActive);
            Assert.Equal(1, session.TotalDrops);

            // Second drop by same user should be blocked by MaxDropsPerUserPerSession limit
            await sut.OnCommand(this, e1);
            Assert.Equal(1, sut.GetCurrentSession()!.TotalDrops);

            // Different user can drop
            var e2 = new CommandEventArgs
            {
                Command = "drop",
                Name = "viewer2",
                DisplayName = "Viewer2",
                UserId = "user-2"
            };
            await sut.OnCommand(this, e2);
            Assert.Equal(2, sut.GetCurrentSession()!.TotalDrops);
        }

        [Fact]
        public async Task OnCommand_DoesNotAutoStart_WhenAutoStartOnCommandIsFalse()
        {
            var settingsService = CreateSettingsService();
            var sut = CreateParachuteService(settingsService);

            var settings = await settingsService.GetSettingsAsync();
            settings.Parachute.SessionModeEnabled = true;
            settings.Parachute.AutoStartOnCommand = false;
            await settingsService.SaveSettingsAsync(settings);

            _commandHandler.Setup(x => x.GetCommand("drop")).Returns(new Command(
                new BaseCommandProperties
                {
                    CommandName = "drop"
                }, sut));

            var e = new CommandEventArgs
            {
                Command = "drop",
                Name = "viewer1",
                DisplayName = "Viewer1",
                UserId = "user-1"
            };

            await sut.OnCommand(this, e);

            var session = sut.GetCurrentSession();
            Assert.Null(session);
            _webSocketMessenger.Verify(x => x.AddToQueue(WsTopics.Parachute, It.Is<string>(j => j.Contains("parachute_drop"))), Times.Never);
        }

        [Fact]
        public async Task StartSessionAsync_AllowsConcurrentDifferentGames_AndQueuesSameGameSequentially()
        {
            var parachuteService = CreateParachuteService();
            var plinkoService = CreatePlinkoService();

            // 1. Start Parachute session (60s)
            var parachute1 = await parachuteService.StartSessionAsync(60, triggeredBy: "Streamer");
            Assert.True(parachute1.IsActive);
            Assert.False(parachute1.IsQueued);
            Assert.Equal(0, parachuteService.GetQueuedSessionCount());

            // 2. Start Plinko session (45s) while Parachute is active -> should ALSO start immediately (concurrent games!)
            var plinko1 = await plinkoService.StartSessionAsync(45, triggeredBy: "Streamer");
            Assert.True(plinko1.IsActive);
            Assert.False(plinko1.IsQueued);
            Assert.Equal(0, plinkoService.GetQueuedSessionCount());

            // Both games are active simultaneously
            Assert.NotNull(parachuteService.GetCurrentSession());
            Assert.NotNull(plinkoService.GetCurrentSession());

            // 3. Start a second Parachute session -> should be queued behind active Parachute
            var parachute2 = await parachuteService.StartSessionAsync(30, triggeredBy: "Mod");
            Assert.False(parachute2.IsActive);
            Assert.True(parachute2.IsQueued);
            Assert.Equal(1, parachute2.QueuePosition);
            Assert.Equal(1, parachuteService.GetQueuedSessionCount());
            Assert.Equal(0, plinkoService.GetQueuedSessionCount());

            // 4. Start a second Plinko session -> should be queued behind active Plinko
            var plinko2 = await plinkoService.StartSessionAsync(25, triggeredBy: "Mod");
            Assert.False(plinko2.IsActive);
            Assert.True(plinko2.IsQueued);
            Assert.Equal(1, plinko2.QueuePosition);
            Assert.Equal(1, plinkoService.GetQueuedSessionCount());

            // 5. Stop the active Parachute session -> second Parachute starts, while Plinko 1 is unaffected
            var stoppedParachute1 = await parachuteService.StopSessionAsync(triggeredBy: "Streamer");
            Assert.NotNull(stoppedParachute1);

            var activeParachute = parachuteService.GetCurrentSession();
            Assert.NotNull(activeParachute);
            Assert.True(activeParachute.IsActive);
            Assert.Equal(30, activeParachute.TotalDurationSeconds);
            Assert.Equal(0, parachuteService.GetQueuedSessionCount());

            // Plinko 1 is still running undisturbed
            var activePlinko = plinkoService.GetCurrentSession();
            Assert.NotNull(activePlinko);
            Assert.Equal(45, activePlinko.TotalDurationSeconds);

            // 6. Stop active Plinko 1 -> second Plinko starts
            var stoppedPlinko1 = await plinkoService.StopSessionAsync(triggeredBy: "Streamer");
            Assert.NotNull(stoppedPlinko1);

            var activePlinko2 = plinkoService.GetCurrentSession();
            Assert.NotNull(activePlinko2);
            Assert.Equal(25, activePlinko2.TotalDurationSeconds);
            Assert.Equal(0, plinkoService.GetQueuedSessionCount());

            // 7. Stop remaining sessions
            await parachuteService.StopSessionAsync(triggeredBy: "Streamer");
            await plinkoService.StopSessionAsync(triggeredBy: "Streamer");
            Assert.Null(parachuteService.GetCurrentSession());
            Assert.Null(plinkoService.GetCurrentSession());
        }

        [Fact]
        public async Task StopSessionAsync_RemovesQueuedSession_WhenGameIsInQueue()
        {
            var sut = CreateParachuteService();

            // Active Parachute session
            await sut.StartSessionAsync(60, triggeredBy: "Streamer");

            // Queued second Parachute session
            var queuedParachute = await sut.StartSessionAsync(45, triggeredBy: "Mod");
            Assert.True(queuedParachute.IsQueued);
            Assert.Equal(1, sut.GetQueuedSessionCount());

            // Explicitly stop active Parachute session
            var stopped = await sut.StopSessionAsync(triggeredBy: "Mod");
            // Since Parachute was active, stopping it promoted the queued one
            var promoted = sut.GetCurrentSession();
            Assert.NotNull(promoted);
            Assert.Equal(45, promoted.TotalDurationSeconds);
            Assert.Equal(0, sut.GetQueuedSessionCount());

            // Stop the promoted session
            await sut.StopSessionAsync(triggeredBy: "Streamer");
            Assert.Null(sut.GetCurrentSession());
        }

        [Fact]
        public async Task ClearSessionQueueAsync_EmptiesQueuedSessions()
        {
            var parachuteService = CreateParachuteService();
            var plinkoService = CreatePlinkoService();

            await parachuteService.StartSessionAsync(60, triggeredBy: "Streamer");
            await parachuteService.StartSessionAsync(30, triggeredBy: "Viewer");
            await plinkoService.StartSessionAsync(60, triggeredBy: "Streamer");
            await plinkoService.StartSessionAsync(40, triggeredBy: "Viewer");

            Assert.Equal(1, parachuteService.GetQueuedSessionCount());
            Assert.Equal(1, plinkoService.GetQueuedSessionCount());

            // Clear Parachute queue
            await parachuteService.ClearSessionQueueAsync();
            Assert.Equal(0, parachuteService.GetQueuedSessionCount());
            Assert.Equal(1, plinkoService.GetQueuedSessionCount());

            // Clear Plinko queue
            await plinkoService.ClearSessionQueueAsync();
            Assert.Equal(0, plinkoService.GetQueuedSessionCount());

            // Stopping active sessions leaves no remaining sessions
            await parachuteService.StopSessionAsync(triggeredBy: "Streamer");
            await plinkoService.StopSessionAsync(triggeredBy: "Streamer");
            Assert.Null(parachuteService.GetCurrentSession());
            Assert.Null(plinkoService.GetCurrentSession());
        }

        [Fact]
        public async Task DropAndPlinko_Commands_OperateIndependentlyDuringConcurrentSessions()
        {
            var settingsService = CreateSettingsService();
            var parachuteService = CreateParachuteService(settingsService);
            var plinkoService = CreatePlinkoService(settingsService);

            var settings = await settingsService.GetSettingsAsync();
            settings.Parachute.SessionModeEnabled = true;
            settings.Parachute.AutoStartOnCommand = true;
            settings.Parachute.DefaultSessionDurationSeconds = 60;
            settings.Plinko.SessionModeEnabled = true;
            settings.Plinko.AutoStartOnCommand = true;
            settings.Plinko.DefaultSessionDurationSeconds = 60;
            await settingsService.SaveSettingsAsync(settings);

            _commandHandler.Setup(x => x.GetCommand("drop")).Returns(new Command(
                new BaseCommandProperties { CommandName = "drop" }, parachuteService));
            _commandHandler.Setup(x => x.GetCommand("plinko")).Returns(new Command(
                new BaseCommandProperties { CommandName = "plinko" }, plinkoService));

            // Start Parachute session
            await parachuteService.StartSessionAsync(60, triggeredBy: "Streamer");

            // User triggers plinko while Parachute is running -> Plinko starts independently!
            var ePlinko = new CommandEventArgs
            {
                Command = "plinko",
                Name = "viewer1",
                DisplayName = "Viewer1",
                UserId = "user-1"
            };
            await plinkoService.OnCommand(this, ePlinko);

            var plinkoSession = plinkoService.GetCurrentSession();
            Assert.NotNull(plinkoSession);
            Assert.True(plinkoSession.IsActive);

            var parachuteSession = parachuteService.GetCurrentSession();
            Assert.NotNull(parachuteSession);
            Assert.True(parachuteSession.IsActive);
        }

        [Fact]
        public void DropRegistry_TryBeginCompletion_And_AcknowledgeCompletion_WorkAsExpected()
        {
            var registry = CreateDropRegistry();
            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = "token-1",
                GameType = DropGameType.Parachute,
                Username = "user1",
                DisplayName = "User1",
                UserId = "user-1",
                EventArgs = new CommandEventArgs { Command = "drop", Name = "user1", DisplayName = "User1", UserId = "user-1" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now.AddSeconds(-2),
                EarliestCompletionUtc = now.AddSeconds(-1),
                ExpiresAtUtc = now.AddSeconds(30)
            };
            registry.RegisterDrop(record);

            // First TryBeginCompletion succeeds
            Assert.True(registry.TryBeginCompletion("token-1", out var beginRecord, out var reason));
            Assert.NotNull(beginRecord);
            Assert.True(beginRecord.IsProcessing);
            Assert.Null(reason);

            // Second concurrent TryBeginCompletion fails because it's in progress
            Assert.False(registry.TryBeginCompletion("token-1", out _, out var reason2));
            Assert.Equal("Drop token completion is already in progress", reason2);

            // If an error happens and processing is reset
            beginRecord.IsProcessing = false;
            Assert.True(registry.TryBeginCompletion("token-1", out _, out _));

            // Acknowledge completion removes the token
            registry.AcknowledgeCompletion("token-1");
            Assert.True(beginRecord.IsCompleted);

            // Subsequent begin completion fails with not found
            Assert.False(registry.TryBeginCompletion("token-1", out _, out var reason3));
            Assert.Contains("Unknown or already-consumed drop token", reason3);
        }

        [Fact]
        public async Task DropGamesSettingsService_DbFailure_ReturnsDefaultsWithoutPersistingOrCaching()
        {
            var failRepo = new Mock<ISettingsRepository>();
            failRepo.Setup(x => x.GetAsync(
                It.IsAny<Expression<Func<Setting, bool>>>(),
                It.IsAny<Func<IQueryable<Setting>, IOrderedQueryable<Setting>>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("DB error"));

            var failUow = new Mock<IUnitOfWork>();
            failUow.Setup(x => x.Settings).Returns(failRepo.Object);

            var failProvider = new Mock<IServiceProvider>();
            failProvider.Setup(x => x.GetService(typeof(IUnitOfWork))).Returns(failUow.Object);

            var failScope = new Mock<IServiceScope>();
            failScope.Setup(x => x.ServiceProvider).Returns(failProvider.Object);

            var failScopeFactory = new Mock<IServiceScopeFactory>();
            failScopeFactory.Setup(x => x.CreateScope()).Returns(failScope.Object);

            var service = new DropGamesSettingsService(failScopeFactory.Object, NullLogger<DropGamesSettingsService>.Instance);
            var settings = await service.GetSettingsAsync();

            Assert.NotNull(settings);
            Assert.NotNull(settings.Parachute);
            Assert.NotEmpty(settings.Themes);
            failUow.Verify(x => x.SaveChangesAsync(), Times.Never);
        }

        [Theory]
        [InlineData(0.50, 100, true, true, true)]    // Dead center -> 100 pts, Bullseye, Win, Hit
        [InlineData(0.59, 1, false, true, true)]     // Exact right edge (0.50 + 0.09) -> 1 pt, Win, Hit
        [InlineData(0.41, 1, false, true, true)]     // Exact left edge (0.50 - 0.09) -> 1 pt, Win, Hit
        [InlineData(0.545, 51, false, true, true)]   // Halfway to edge (0.045 / 0.09 = 0.5) -> 51 pts
        [InlineData(0.60, 0, false, false, false)]   // Missed platform right -> 0 pts, No Win, No Hit
        [InlineData(0.40, 0, false, false, false)]   // Missed platform left -> 0 pts, No Win, No Hit
        public void ParachuteSimulation_ScoreCalculation_FollowsZeroToHundredRule(
            double landingX, int expectedScore, bool expectedBullseye, bool expectedWin, bool expectedHit)
        {
            var (score, isBullseye, isWin, isHit) =
                ParachuteSimulationEngine.CalculateLandingScore(landingX, 0.50, 0.09);

            Assert.Equal(expectedScore, score);
            Assert.Equal(expectedBullseye, isBullseye);
            Assert.Equal(expectedWin, isWin);
            Assert.Equal(expectedHit, isHit);
        }

        [Fact]
        public void ParachuteSimulation_ReachesLandingY_WithSufficientTickBudget()
        {
            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = "landing-test-token",
                GameType = DropGameType.Parachute,
                Username = "testuser",
                DisplayName = "TestUser",
                UserId = "user-1",
                EventArgs = new CommandEventArgs { Command = "drop", Name = "testuser", DisplayName = "TestUser", UserId = "user-1" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now,
                EarliestCompletionUtc = now,
                ExpiresAtUtc = now.AddMinutes(1),
                StartX = 0.50,
                StartY = 0.04,
                LandingY = 0.88,
                InitialVx = 0.0,
                InitialVy = 0.20,
                WindAccelX = 0.0,
                TotalTicks = 300,
                TargetCenterX = 0.50,
                TargetHalfWidth = 0.09
            };

            ParachuteSimulationEngine.SimulateParachuteTrajectory(record, 0, []);

            Assert.True(record.Keyframes.Count > 0);
            var lastKeyframe = record.Keyframes[^1];
            Assert.True(lastKeyframe.Y >= record.LandingY);
            Assert.Equal(100, record.Score);
            Assert.True(record.IsBullseye);
            Assert.True(record.IsWin);
            Assert.True(record.IsHit);
        }

        [Fact]
        public void ParachuteSimulation_DirectionalSpeedAndWallBounce_ReflectsWithoutSway()
        {
            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = "wall-bounce-token",
                GameType = DropGameType.Parachute,
                Username = "testuser",
                DisplayName = "TestUser",
                UserId = "user-1",
                EventArgs = new CommandEventArgs { Command = "drop", Name = "testuser", DisplayName = "TestUser", UserId = "user-1" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now,
                EarliestCompletionUtc = now,
                ExpiresAtUtc = now.AddMinutes(1),
                StartX = 0.90,
                StartY = 0.04,
                LandingY = 0.88,
                InitialVx = 0.15, // Fast sharp rightward glide
                InitialVy = 0.10,
                WindAccelX = 0.0,
                SwayAmplitude = 0.0,
                SwayFrequency = 0.0,
                SwayPhase = 0.0,
                TotalTicks = 120,
                TargetCenterX = 0.50,
                TargetHalfWidth = 0.09
            };

            ParachuteSimulationEngine.SimulateParachuteTrajectory(record, 0, []);

            // Parachuter should hit the right wall margin (0.95), reflect leftward, and land with negative horizontal velocity
            Assert.Contains(record.Keyframes, k => k.X >= 0.90);
            Assert.Contains(record.Keyframes, k => k.Vx < 0);
            Assert.True(record.LandingX < 0.95);
            Assert.True(record.LandingX > 0.05);
        }

        [Fact]
        public async Task DropGameCompleteHandler_Parachute_RecalculatesScoreWithReportedLandingX()
        {
            var dropRegistry = CreateDropRegistry();
            var settingsService = CreateSettingsService();
            var parachuteServiceMock = new Mock<IParachuteDropService>();
            var plinkoServiceMock = new Mock<IPlinkoDropService>();
            var coordinatorMock = new Mock<IFeatureRuntimeCoordinator>();
            coordinatorMock.Setup(c => c.IsEnabled(FeatureKeys.DropGames)).Returns(true);

            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = "para-complete-token",
                GameType = DropGameType.Parachute,
                Username = "testuser",
                DisplayName = "TestUser",
                UserId = "user-1",
                EventArgs = new CommandEventArgs { Command = "drop", Name = "testuser", DisplayName = "TestUser", UserId = "user-1" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now.AddSeconds(-5),
                EarliestCompletionUtc = now.AddSeconds(-1),
                ExpiresAtUtc = now.AddMinutes(1),
                StartX = 0.20,
                StartY = 0.04,
                LandingY = 0.88,
                InitialVx = 0.0,
                InitialVy = 0.10,
                WindAccelX = 0.0,
                SwayAmplitude = 0.0,
                SwayFrequency = 0.0,
                SwayPhase = 0.0,
                TotalTicks = 60,
                TargetCenterX = 0.50,
                TargetHalfWidth = 0.10,
                LandingX = 0.42,
                Score = 20,
                IsHit = true,
                IsBullseye = false,
                IsWin = true
            };

            dropRegistry.RegisterDrop(record);

            var handler = new DropGameCompleteHandler(
                dropRegistry,
                parachuteServiceMock.Object,
                plinkoServiceMock.Object,
                settingsService,
                coordinatorMock.Object);

            // Viewer bounced within tolerance (0.50 vs 0.42 = 0.08 <= 0.15) and landed dead center!
            var notification = new DropGameCompleteNotification("para-complete-token", landingX: 0.50);
            await handler.Handle(notification, CancellationToken.None);

            parachuteServiceMock.Verify(s => s.ProcessCompletionAsync(It.Is<PendingDropRecord>(r =>
                r.DropToken == "para-complete-token" &&
                r.LandingX == 0.50 &&
                r.Score == 100 &&
                r.IsBullseye &&
                r.IsWin &&
                r.IsHit)), Times.Once);
        }

        [Fact]
        public async Task DropGameCompleteHandler_Parachute_OutOfToleranceLandingX_PreservesServerOutcome()
        {
            var dropRegistry = CreateDropRegistry();
            var settingsService = CreateSettingsService();
            var parachuteServiceMock = new Mock<IParachuteDropService>();
            var plinkoServiceMock = new Mock<IPlinkoDropService>();
            var coordinatorMock = new Mock<IFeatureRuntimeCoordinator>();
            coordinatorMock.Setup(c => c.IsEnabled(FeatureKeys.DropGames)).Returns(true);

            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = "para-out-of-bounds-token",
                GameType = DropGameType.Parachute,
                Username = "testuser",
                DisplayName = "TestUser",
                UserId = "user-1",
                EventArgs = new CommandEventArgs { Command = "drop", Name = "testuser", DisplayName = "TestUser", UserId = "user-1" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now.AddSeconds(-5),
                EarliestCompletionUtc = now.AddSeconds(-1),
                ExpiresAtUtc = now.AddMinutes(1),
                StartX = 0.20,
                StartY = 0.06,
                LandingY = 0.88,
                TargetCenterX = 0.50,
                TargetHalfWidth = 0.10,
                LandingX = 0.20,
                Score = 0,
                IsHit = false,
                IsBullseye = false,
                IsWin = false
            };

            dropRegistry.RegisterDrop(record);

            var handler = new DropGameCompleteHandler(
                dropRegistry,
                parachuteServiceMock.Object,
                plinkoServiceMock.Object,
                settingsService,
                coordinatorMock.Object);

            // Client reports 0.50 but server computed 0.20 (diff 0.30 > 0.15 tolerance) -> server values preserved
            var notification = new DropGameCompleteNotification("para-out-of-bounds-token", landingX: 0.50);
            await handler.Handle(notification, CancellationToken.None);

            parachuteServiceMock.Verify(s => s.ProcessCompletionAsync(It.Is<PendingDropRecord>(r =>
                r.DropToken == "para-out-of-bounds-token" &&
                r.LandingX == 0.20 &&
                r.Score == 0 &&
                !r.IsWin)), Times.Once);
        }

        [Fact]
        public async Task DropGameCompleteHandler_Plinko_RecalculatesOutcomeWithReportedBucketIndex()
        {
            var dropRegistry = CreateDropRegistry();
            var settingsService = CreateSettingsService();
            var parachuteServiceMock = new Mock<IParachuteDropService>();
            var plinkoServiceMock = new Mock<IPlinkoDropService>();
            var coordinatorMock = new Mock<IFeatureRuntimeCoordinator>();
            coordinatorMock.Setup(c => c.IsEnabled(FeatureKeys.DropGames)).Returns(true);

            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = "plinko-complete-token",
                GameType = DropGameType.Plinko,
                Username = "testuser",
                DisplayName = "TestUser",
                UserId = "user-1",
                EventArgs = new CommandEventArgs { Command = "plinko", Name = "testuser", DisplayName = "TestUser", UserId = "user-1" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now.AddSeconds(-5),
                EarliestCompletionUtc = now.AddSeconds(-1),
                ExpiresAtUtc = now.AddMinutes(1),
                StartX = 0.50,
                StartY = 0.04,
                LandingY = 0.88,
                InitialVx = 0.0,
                InitialVy = 0.10,
                WindAccelX = 0.0,
                SwayAmplitude = 0.0,
                SwayFrequency = 0.0,
                SwayPhase = 0.0,
                TotalTicks = 60,
                TargetCenterX = 0.50,
                TargetHalfWidth = 0.10,
                BucketIndex = 1,
                BucketLabel = "Low Win",
                Score = 10,
                IsWin = true
            };

            dropRegistry.RegisterDrop(record);

            var handler = new DropGameCompleteHandler(
                dropRegistry,
                parachuteServiceMock.Object,
                plinkoServiceMock.Object,
                settingsService,
                coordinatorMock.Object);

            // Viewer bumped into bucket 2 within tolerance (Math.Abs(2 - 1) == 1 <= 1)
            var notification = new DropGameCompleteNotification("plinko-complete-token", winningBucketIndex: 2);
            await handler.Handle(notification, CancellationToken.None);

            plinkoServiceMock.Verify(s => s.ProcessCompletionAsync(It.Is<PendingDropRecord>(r =>
                r.DropToken == "plinko-complete-token" &&
                r.BucketIndex == 2 &&
                r.Score == 100 &&
                r.IsWin)), Times.Once);
        }

        [Fact]
        public async Task DropGameCompleteHandler_Plinko_OutOfToleranceBucketIndex_PreservesServerOutcome()
        {
            var dropRegistry = CreateDropRegistry();
            var settingsService = CreateSettingsService();
            var parachuteServiceMock = new Mock<IParachuteDropService>();
            var plinkoServiceMock = new Mock<IPlinkoDropService>();
            var coordinatorMock = new Mock<IFeatureRuntimeCoordinator>();
            coordinatorMock.Setup(c => c.IsEnabled(FeatureKeys.DropGames)).Returns(true);

            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = "plinko-out-of-bounds-token",
                GameType = DropGameType.Plinko,
                Username = "testuser",
                DisplayName = "TestUser",
                UserId = "user-1",
                EventArgs = new CommandEventArgs { Command = "plinko", Name = "testuser", DisplayName = "TestUser", UserId = "user-1" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now.AddSeconds(-5),
                EarliestCompletionUtc = now.AddSeconds(-1),
                ExpiresAtUtc = now.AddMinutes(1),
                StartX = 0.50,
                StartY = 0.04,
                LandingY = 0.88,
                TargetCenterX = 0.50,
                TargetHalfWidth = 0.10,
                BucketIndex = 0,
                BucketLabel = "Loss",
                Score = 0,
                IsWin = false
            };

            dropRegistry.RegisterDrop(record);

            var handler = new DropGameCompleteHandler(
                dropRegistry,
                parachuteServiceMock.Object,
                plinkoServiceMock.Object,
                settingsService,
                coordinatorMock.Object);

            // Client reports 2 but server was 0 (diff = 2 > 1 tolerance) -> server outcome preserved
            var notification = new DropGameCompleteNotification("plinko-out-of-bounds-token", winningBucketIndex: 2);
            await handler.Handle(notification, CancellationToken.None);

            plinkoServiceMock.Verify(s => s.ProcessCompletionAsync(It.Is<PendingDropRecord>(r =>
                r.DropToken == "plinko-out-of-bounds-token" &&
                r.BucketIndex == 0 &&
                r.Score == 0 &&
                !r.IsWin)), Times.Once);
        }

        [Fact]
        public async Task ResolveMediaUrlsAsync_MultipleEmotesAndUrls_ResolvesAllValidItems()
        {
            var emoteMock = new Mock<IEmoteService>();
            var cachedMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sptvShots"] = "https://static-cdn.jtvnw.net/emoticons/v2/em1/default/dark/2.0",
                ["sptvLights"] = "https://static-cdn.jtvnw.net/emoticons/v2/em2/default/dark/2.0"
            };
            emoteMock.Setup(e => e.GetEmotesAsync()).ReturnsAsync(cachedMap);
            emoteMock.Setup(e => e.ResolveEmoteUrlAsync("sptvShots"))
                .ReturnsAsync("https://static-cdn.jtvnw.net/emoticons/v2/em1/default/dark/2.0");
            emoteMock.Setup(e => e.ResolveEmoteUrlAsync("sptvLights"))
                .ReturnsAsync("https://static-cdn.jtvnw.net/emoticons/v2/em2/default/dark/2.0");
            emoteMock.Setup(e => e.ResolveEmoteUrlAsync("unknownEmote"))
                .ReturnsAsync((string?)null);

            var eventArgs = new CommandEventArgs { Arg = "" };
            var rawInput = "sptvShots, https://example.com/custom.png; sptvLights unknownEmote";

            var results = await DropGameMediaResolver.ResolveMediaUrlsAsync(
                emoteMock.Object, eventArgs, rawInput, fallbackInput: null, allowDirectUrlOrPath: true);

            Assert.Equal(3, results.Count);
            Assert.Contains("https://static-cdn.jtvnw.net/emoticons/v2/em1/default/dark/2.0", results);
            Assert.Contains("https://example.com/custom.png", results);
            Assert.Contains("https://static-cdn.jtvnw.net/emoticons/v2/em2/default/dark/2.0", results);
            Assert.DoesNotContain("unknownEmote", results);
        }

        [Fact]
        public async Task ResolveCharacterOrEmoteUrlAsync_MultipleEmoteNames_PicksFromPool()
        {
            var emoteMock = new Mock<IEmoteService>();
            var cachedMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sptvShots"] = "https://static-cdn.jtvnw.net/emoticons/v2/em1/default/dark/2.0",
                ["sptvLights"] = "https://static-cdn.jtvnw.net/emoticons/v2/em2/default/dark/2.0"
            };
            emoteMock.Setup(e => e.GetEmotesAsync()).ReturnsAsync(cachedMap);
            emoteMock.Setup(e => e.ResolveEmoteUrlAsync("sptvShots"))
                .ReturnsAsync("https://static-cdn.jtvnw.net/emoticons/v2/em1/default/dark/2.0");
            emoteMock.Setup(e => e.ResolveEmoteUrlAsync("sptvLights"))
                .ReturnsAsync("https://static-cdn.jtvnw.net/emoticons/v2/em2/default/dark/2.0");

            var eventArgs = new CommandEventArgs { Arg = "sptvShots, sptvLights" };

            var picked = await DropGameMediaResolver.ResolveCharacterOrEmoteUrlAsync(
                emoteMock.Object, eventArgs, emoteOrImageInput: null, fallbackUrl: null, allowDirectUrlOrPath: false);

            Assert.True(
                picked == "https://static-cdn.jtvnw.net/emoticons/v2/em1/default/dark/2.0" ||
                picked == "https://static-cdn.jtvnw.net/emoticons/v2/em2/default/dark/2.0");
        }
    }
}
