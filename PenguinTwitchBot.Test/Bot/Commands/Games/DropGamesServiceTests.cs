using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PenguinTwitchBot.Application.DropGameNotifications;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Actions.Triggers;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Bot.Commands.Games.Parachute;
using PenguinTwitchBot.Bot.Commands.Games.Plinko;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Models.Chat;
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
            Assert.Equal(5, settings.Plinko.Buckets.Count);
            Assert.True(settings.Plinko.Buckets[2].IsWin);
            Assert.Equal("WINNER", settings.Plinko.Buckets[2].Label);
            Assert.Equal(0, settings.Plinko.Buckets[2].Score);
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

            // 3. Direct custom URL or path is resolved
            var resolvedDirect = await DropGameMediaResolver.ResolveCharacterOrEmoteUrlAsync(_emoteService.Object, null, "/media/custom_chibi.png", "/fallback.png", allowDirectUrlOrPath: true);
            Assert.Equal("/media/custom_chibi.png", resolvedDirect);

            // 3b. Cross-channel or special event emote in Fragments is resolved as provided even if not in global/channel emote cache
            var specialEventArgs = new CommandEventArgs
            {
                Name = "viewer1",
                DisplayName = "Viewer1",
                Arg = "HyperspecialEmote",
                Fragments =
                [
                    new ChatOverlayFragment
                    {
                        Type = "emote",
                        Text = "HyperspecialEmote",
                        EmoteId = "emotesv2_special999",
                        EmoteUrl = "https://static-cdn.jtvnw.net/emoticons/v2/emotesv2_special999/default/dark/1.0"
                    }
                ]
            };
            var resolvedSpecial = await DropGameMediaResolver.ResolveCharacterOrEmoteUrlAsync(_emoteService.Object, specialEventArgs, null, "/fallback.png");
            Assert.Equal("https://static-cdn.jtvnw.net/emoticons/v2/emotesv2_special999/default/dark/1.0", resolvedSpecial);

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

            var winBucketIndex = settings.Plinko.Buckets.FindIndex(b => b.IsWin);
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
                b.IsWin = b.Label == "WINNER";
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
            Assert.Equal(5, reloaded.Plinko.Buckets.Count);
            var singleWin = Assert.Single(reloaded.Plinko.Buckets, b => b.IsWin);
            Assert.Equal("WINNER", singleWin.Label);

            // Force landing in index 1 ("MISS", where IsWin == false)
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
        public async Task DropGameCompleteHandler_Parachute_LargeDeflectionLandingX_RecalculatesWinOutcome()
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
                DropToken = "para-large-deflection-token",
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

            // Client reports 0.50 after a big droplet/mid-air bounce from 0.20 -> outcome is recalculated as a win
            var notification = new DropGameCompleteNotification("para-large-deflection-token", landingX: 0.50);
            await handler.Handle(notification, CancellationToken.None);

            parachuteServiceMock.Verify(s => s.ProcessCompletionAsync(It.Is<PendingDropRecord>(r =>
                r.DropToken == "para-large-deflection-token" &&
                r.LandingX == 0.50 &&
                r.Score == 100 &&
                r.IsBullseye &&
                r.IsWin)), Times.Once);
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

            // Viewer bumped into bucket 2
            var notification = new DropGameCompleteNotification("plinko-complete-token", winningBucketIndex: 2);
            await handler.Handle(notification, CancellationToken.None);

            plinkoServiceMock.Verify(s => s.ProcessCompletionAsync(It.Is<PendingDropRecord>(r =>
                r.DropToken == "plinko-complete-token" &&
                r.BucketIndex == 2 &&
                r.BucketLabel == "WINNER" &&
                r.Score == 0 &&
                r.IsWin)), Times.Once);
        }

        [Fact]
        public async Task DropGameCompleteHandler_Plinko_MultiBucketDeflection_RecalculatesWinOutcome()
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
                DropToken = "plinko-multi-bucket-token",
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

            // Client reports bucket 2 after bouncing across multiple buckets from 0 -> recalculated as win
            var notification = new DropGameCompleteNotification("plinko-multi-bucket-token", winningBucketIndex: 2);
            await handler.Handle(notification, CancellationToken.None);

            plinkoServiceMock.Verify(s => s.ProcessCompletionAsync(It.Is<PendingDropRecord>(r =>
                r.DropToken == "plinko-multi-bucket-token" &&
                r.BucketIndex == 2 &&
                r.BucketLabel == "WINNER" &&
                r.IsWin)), Times.Once);
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

        [Fact]
        public void PlinkoGameSettings_DefaultBuckets_HasFiveSlotsWithCenterWinner()
        {
            var buckets = PlinkoGameSettings.CreateDefaultBuckets();
            Assert.Equal(5, buckets.Count);

            Assert.False(buckets[0].IsWin);
            Assert.Equal(0, buckets[0].Score);
            Assert.Equal("MISS", buckets[0].Label);

            Assert.False(buckets[1].IsWin);
            Assert.Equal(0, buckets[1].Score);
            Assert.Equal("MISS", buckets[1].Label);

            Assert.True(buckets[2].IsWin);
            Assert.Equal(0, buckets[2].Score);
            Assert.Equal("WINNER", buckets[2].Label);

            Assert.False(buckets[3].IsWin);
            Assert.Equal(0, buckets[3].Score);
            Assert.Equal("MISS", buckets[3].Label);

            Assert.False(buckets[4].IsWin);
            Assert.Equal(0, buckets[4].Score);
            Assert.Equal("MISS", buckets[4].Label);
        }

        [Fact]
        public void BuildPlinkoWaypoints_ProducesOrganicLandingSpreadWithinTargetBucketSlot()
        {
            const int rows = 8;
            const int bucketCount = 5;
            const int targetBucketIndex = 2; // Center slot: bounds are [2/5, 3/5] = [0.40, 0.60]

            var landedPositions = new HashSet<double>();
            for (var i = 0; i < 20; i++)
            {
                var waypoints = PlinkoSimulationEngine.BuildPlinkoWaypoints(rows, bucketCount, targetBucketIndex);
                Assert.NotNull(waypoints);
                Assert.Equal(rows + 2, waypoints.Count);

                var finalWaypoint = waypoints.Last();
                Assert.Equal(rows, finalWaypoint.Row);
                Assert.Equal(targetBucketIndex, finalWaypoint.Col);
                Assert.Equal(0.96, finalWaypoint.Y);

                // Must be safely within the target bucket slot bounds [0.40, 0.60]
                Assert.InRange(finalWaypoint.X, 0.40, 0.60);
                landedPositions.Add(finalWaypoint.X);

                // Verify every intermediate peg waypoint (r = 0..rows-1) lands on a physical peg coordinate
                var cols = Math.Max(6, bucketCount);
                for (var r = 0; r < rows; r++)
                {
                    var wp = waypoints[r + 1];
                    Assert.Equal(r, wp.Row);
                    var isOdd = r % 2 == 1;
                    var countInRow = isOdd ? cols - 1 : cols;
                    Assert.InRange(wp.Col, 0, countInRow - 1);
                    var expectedX = Math.Round(isOdd ? (wp.Col + 1.0) / cols : (wp.Col + 0.5) / cols, 4);
                    var expectedY = Math.Round(0.16 + (((double)(r + 1) / rows) * 0.64), 4);
                    Assert.Equal(expectedX, wp.X);
                    Assert.Equal(expectedY, wp.Y);
                }
            }

            // Verify the landings are naturally distributed rather than all snapped to identical center (0.50)
            Assert.True(landedPositions.Count > 1, "Landings should spread naturally across the bucket width instead of snapping to a single static center");
        }

        [Fact]
        public async Task ProcessCompletionAsync_Parachute_WinActionThrows_DoesNotAbortCompletion()
        {
            var settingsService = CreateSettingsService();
            var registry = CreateDropRegistry();
            var sut = CreateParachuteService(settingsService, registry);

            var settings = await settingsService.GetSettingsAsync();
            settings.Parachute.WinActionName = "FailedAction";
            settings.Parachute.AnnounceWinsInChat = true;
            await settingsService.SaveSettingsAsync(settings);

            var rewardAction = new ActionType { Name = "FailedAction", Enabled = true };
            _actionManagementService.Setup(x => x.GetAllActionsAsync()).ReturnsAsync([rewardAction]);
            _actionService.Setup(x => x.EnqueueAction(It.IsAny<ConcurrentDictionary<string, string>>(), It.IsAny<ActionType>()))
                .ThrowsAsync(new InvalidOperationException("Queue is full"));

            var token = await sut.TriggerParachuteDropAsync(
                new CommandEventArgs { Name = "dropwinner", DisplayName = "DropWinner", UserId = "6001" },
                forcedStartX: 0.50,
                forcedWindAccelX: 0.0,
                skipMinimumDurationCheck: true);

            var consumed = registry.TryConsumeDrop(token, out var record, out _);
            Assert.True(consumed);
            Assert.NotNull(record);
            Assert.True(record.IsWin);

            // Should not throw even when EnqueueAction throws
            var ex = await Record.ExceptionAsync(() => sut.ProcessCompletionAsync(record));
            Assert.Null(ex);

            // Win chat announcement should still execute
            Assert.True(record.WinChatAnnounced);
            _serviceBackbone.Verify(x => x.SendChatMessage(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ProcessCompletionAsync_Plinko_WinActionThrows_DoesNotAbortCompletion()
        {
            var settingsService = CreateSettingsService();
            var registry = CreateDropRegistry();
            var sut = CreatePlinkoService(settingsService, registry);

            var settings = await settingsService.GetSettingsAsync();
            settings.Plinko.WinActionName = "FailedPlinkoAction";
            settings.Plinko.AnnounceWinsInChat = true;
            await settingsService.SaveSettingsAsync(settings);

            var plinkoAction = new ActionType { Name = "FailedPlinkoAction", Enabled = true };
            _actionManagementService.Setup(x => x.GetAllActionsAsync()).ReturnsAsync([plinkoAction]);
            _actionService.Setup(x => x.EnqueueAction(It.IsAny<ConcurrentDictionary<string, string>>(), It.IsAny<ActionType>()))
                .ThrowsAsync(new InvalidOperationException("Queue is full"));

            var winIndex = settings.Plinko.Buckets.FindIndex(b => b.IsWin);
            var token = await sut.TriggerPlinkoDropAsync(
                new CommandEventArgs { Name = "plinkowinner", DisplayName = "PlinkoWinner", UserId = "6002" },
                forcedBucketIndex: winIndex,
                skipMinimumDurationCheck: true);

            var consumed = registry.TryConsumeDrop(token, out var record, out _);
            Assert.True(consumed);
            Assert.NotNull(record);
            Assert.True(record.IsWin);

            // Should not throw even when EnqueueAction throws
            var ex = await Record.ExceptionAsync(() => sut.ProcessCompletionAsync(record));
            Assert.Null(ex);

            // Win chat announcement should still execute
            Assert.True(record.WinChatAnnounced);
            _serviceBackbone.Verify(x => x.SendChatMessage(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void DropRegistry_SetSessionActive_TracksStateCorrectly()
        {
            var registry = CreateDropRegistry();

            Assert.False(registry.IsSessionActive(DropGameType.Parachute));
            Assert.False(registry.IsSessionActive(DropGameType.Plinko));

            registry.SetSessionActive(DropGameType.Parachute, true);
            Assert.True(registry.IsSessionActive(DropGameType.Parachute));
            Assert.False(registry.IsSessionActive(DropGameType.Plinko));

            registry.SetSessionActive(DropGameType.Plinko, true);
            Assert.True(registry.IsSessionActive(DropGameType.Parachute));
            Assert.True(registry.IsSessionActive(DropGameType.Plinko));

            registry.SetSessionActive(DropGameType.Parachute, false);
            Assert.False(registry.IsSessionActive(DropGameType.Parachute));
            Assert.True(registry.IsSessionActive(DropGameType.Plinko));
        }

        [Fact]
        public void ParachuteAndPlinkoSettings_HeaderDefaults_AreConfiguredCorrectly()
        {
            var parachute = new ParachuteGameSettings();
            Assert.True(parachute.AutoAvoidOverlap);
            Assert.True(parachute.ShowHeaderBanner);
            Assert.True(parachute.ShowTimer);
            Assert.True(parachute.ShowDropCount);
            Assert.True(parachute.ShowFreePlayCounter);
            Assert.Equal("TopCenter", parachute.HeaderPosition);
            Assert.Equal(18, parachute.HeaderCustomYOffset);
            Assert.Equal(18, parachute.NameFontSize);

            var plinko = new PlinkoGameSettings();
            Assert.True(plinko.AutoAvoidOverlap);
            Assert.True(plinko.ShowHeaderBanner);
            Assert.True(plinko.ShowTimer);
            Assert.True(plinko.ShowTokenCount);
            Assert.True(plinko.ShowFreePlayCounter);
            Assert.Equal("TopCenter", plinko.HeaderPosition);
            Assert.Equal(18, plinko.HeaderCustomYOffset);
            Assert.Equal(18, plinko.NameFontSize);
        }

        [Fact]
        public async Task ParachuteDropService_StartSessionAsync_IncludesHeaderConfigAndOtherGameStatus()
        {
            var settingsService = CreateSettingsService();
            var registry = CreateDropRegistry();
            var sut = CreateParachuteService(settingsService, registry);

            // Simulate Plinko session active
            registry.SetSessionActive(DropGameType.Plinko, true);

            string? broadcastPayload = null;
            _webSocketMessenger.Setup(x => x.AddToQueue(WsTopics.Parachute, It.IsAny<string>()))
                .Callback<string, string>((_, p) => broadcastPayload = p)
                .Returns(Task.CompletedTask);

            var session = await sut.StartSessionAsync(durationSeconds: 60);

            Assert.True(session.IsActive);
            Assert.True(session.IsOtherGameActive);
            Assert.NotNull(session.HeaderConfig);
            Assert.True(session.HeaderConfig.AutoAvoidOverlap);
            Assert.True(session.HeaderConfig.ShowBanner);
            Assert.True(session.HeaderConfig.ShowTimer);
            Assert.True(session.HeaderConfig.ShowCount);
            Assert.Equal("TopCenter", session.HeaderConfig.Position);
            Assert.Equal(18, session.HeaderConfig.YOffset);

            Assert.NotNull(broadcastPayload);
            Assert.Contains("\"headerConfig\"", broadcastPayload);
            Assert.Contains("\"isOtherGameActive\":true", broadcastPayload);
        }

        [Fact]
        public async Task PlinkoDropService_StartSessionAsync_IncludesHeaderConfigAndOtherGameStatus()
        {
            var settingsService = CreateSettingsService();
            var registry = CreateDropRegistry();
            var sut = CreatePlinkoService(settingsService, registry);

            // Simulate Parachute session active
            registry.SetSessionActive(DropGameType.Parachute, true);

            string? broadcastPayload = null;
            _webSocketMessenger.Setup(x => x.AddToQueue(WsTopics.Plinko, It.IsAny<string>()))
                .Callback<string, string>((_, p) => broadcastPayload = p)
                .Returns(Task.CompletedTask);

            var session = await sut.StartSessionAsync(durationSeconds: 60);

            Assert.True(session.IsActive);
            Assert.True(session.IsOtherGameActive);
            Assert.NotNull(session.HeaderConfig);
            Assert.True(session.HeaderConfig.AutoAvoidOverlap);
            Assert.True(session.HeaderConfig.ShowBanner);
            Assert.True(session.HeaderConfig.ShowTimer);
            Assert.True(session.HeaderConfig.ShowCount);
            Assert.Equal("TopCenter", session.HeaderConfig.Position);
            Assert.Equal(18, session.HeaderConfig.YOffset);

            Assert.NotNull(broadcastPayload);
            Assert.Contains("\"headerConfig\"", broadcastPayload);
            Assert.Contains("\"isOtherGameActive\":true", broadcastPayload);
        }

        [Fact]
        public async Task DropServices_StartAndStopSessionAsync_NotifiesCrossGameTopicOfOtherGameStatus()
        {
            var settingsService = CreateSettingsService();
            var registry = CreateDropRegistry();
            var parachuteSut = CreateParachuteService(settingsService, registry);
            var plinkoSut = CreatePlinkoService(settingsService, registry);

            var capturedPlinkoNotices = new List<string>();
            _webSocketMessenger.Setup(x => x.AddToQueue(WsTopics.Plinko, It.Is<string>(p => p.Contains("other_game_status"))))
                .Callback<string, string>((_, p) => capturedPlinkoNotices.Add(p))
                .Returns(Task.CompletedTask);

            var capturedParachuteNotices = new List<string>();
            _webSocketMessenger.Setup(x => x.AddToQueue(WsTopics.Parachute, It.Is<string>(p => p.Contains("other_game_status"))))
                .Callback<string, string>((_, p) => capturedParachuteNotices.Add(p))
                .Returns(Task.CompletedTask);

            // Starting Parachute notifies Plinko topic
            await parachuteSut.StartSessionAsync(durationSeconds: 30);
            Assert.Single(capturedPlinkoNotices);
            Assert.Contains("\"isOtherGameActive\":true", capturedPlinkoNotices[0]);
            Assert.Contains("\"otherGame\":\"parachute\"", capturedPlinkoNotices[0]);

            // Stopping Parachute notifies Plinko topic
            await parachuteSut.StopSessionAsync(triggeredBy: "moderator");
            Assert.Equal(2, capturedPlinkoNotices.Count);
            Assert.Contains("\"isOtherGameActive\":false", capturedPlinkoNotices[1]);
            Assert.Contains("\"otherGame\":\"parachute\"", capturedPlinkoNotices[1]);

            // Starting Plinko notifies Parachute topic
            await plinkoSut.StartSessionAsync(durationSeconds: 30);
            Assert.Single(capturedParachuteNotices);
            Assert.Contains("\"isOtherGameActive\":true", capturedParachuteNotices[0]);
            Assert.Contains("\"otherGame\":\"plinko\"", capturedParachuteNotices[0]);

            // Stopping Plinko notifies Parachute topic
            await plinkoSut.StopSessionAsync(triggeredBy: "moderator");
            Assert.Equal(2, capturedParachuteNotices.Count);
            Assert.Contains("\"isOtherGameActive\":false", capturedParachuteNotices[1]);
            Assert.Contains("\"otherGame\":\"plinko\"", capturedParachuteNotices[1]);
        }

        [Fact]
        public async Task ParachuteDropService_TriggerDropAndDroplets_NotifiesPlinkoTopicOfDropActivity()
        {
            var settingsService = CreateSettingsService();
            var registry = CreateDropRegistry();
            var parachuteSut = CreateParachuteService(settingsService, registry);

            var capturedPlinkoActivityNotices = new List<string>();
            _webSocketMessenger.Setup(x => x.AddToQueue(WsTopics.Plinko, It.Is<string>(p => p.Contains("other_drop_active"))))
                .Callback<string, string>((_, p) => capturedPlinkoActivityNotices.Add(p))
                .Returns(Task.CompletedTask);

            await parachuteSut.TriggerParachuteDropAsync(new CommandEventArgs { Name = "viewer1", DisplayName = "Viewer1" });
            Assert.Single(capturedPlinkoActivityNotices);
            Assert.Contains("\"type\":\"other_drop_active\"", capturedPlinkoActivityNotices[0]);
            Assert.Contains("\"durationSeconds\"", capturedPlinkoActivityNotices[0]);

            await parachuteSut.SpawnDropletsAsync(new DropletSpawnRequest { Count = 5 });
            Assert.Equal(2, capturedPlinkoActivityNotices.Count);
            Assert.Contains("\"type\":\"other_drop_active\"", capturedPlinkoActivityNotices[1]);
        }

        [Fact]
        public async Task DropServices_TriggerDrops_TransmitHeaderConfig()
        {
            var settingsService = CreateSettingsService();
            var registry = CreateDropRegistry();
            var parachuteSut = CreateParachuteService(settingsService, registry);
            var plinkoSut = CreatePlinkoService(settingsService, registry);

            string? parachuteDropPayload = null;
            _webSocketMessenger.Setup(x => x.AddToQueue(WsTopics.Parachute, It.IsAny<string>()))
                .Callback<string, string>((_, p) => parachuteDropPayload = p)
                .Returns(Task.CompletedTask);

            await parachuteSut.TriggerParachuteDropAsync(new CommandEventArgs { Name = "viewer1", DisplayName = "Viewer1" });
            Assert.NotNull(parachuteDropPayload);
            Assert.Contains("\"headerConfig\"", parachuteDropPayload);

            string? plinkoDropPayload = null;
            _webSocketMessenger.Setup(x => x.AddToQueue(WsTopics.Plinko, It.IsAny<string>()))
                .Callback<string, string>((_, p) => plinkoDropPayload = p)
                .Returns(Task.CompletedTask);

            await plinkoSut.TriggerPlinkoDropAsync(new CommandEventArgs { Name = "viewer2", DisplayName = "Viewer2" });
            Assert.NotNull(plinkoDropPayload);
            Assert.Contains("\"headerConfig\"", plinkoDropPayload);
        }

        [Fact]
        public async Task DropServices_TriggerDrops_IncludeUserColorInPayload()
        {
            var settingsService = CreateSettingsService();
            var registry = CreateDropRegistry();
            var parachuteSut = CreateParachuteService(settingsService, registry);
            var plinkoSut = CreatePlinkoService(settingsService, registry);

            string? parachuteDropPayload = null;
            _webSocketMessenger.Setup(x => x.AddToQueue(WsTopics.Parachute, It.IsAny<string>()))
                .Callback<string, string>((_, p) => parachuteDropPayload = p)
                .Returns(Task.CompletedTask);

            // Test explicit twitch color on CommandEventArgs
            await parachuteSut.TriggerParachuteDropAsync(new CommandEventArgs
            {
                Name = "viewer1",
                DisplayName = "Viewer1",
                UserId = "12345",
                Color = "#1E90FF"
            });
            Assert.NotNull(parachuteDropPayload);
            Assert.Contains("\"userColor\":\"#1E90FF\"", parachuteDropPayload);
            Assert.Contains("\"color\":\"#1E90FF\"", parachuteDropPayload);

            string? plinkoDropPayload = null;
            _webSocketMessenger.Setup(x => x.AddToQueue(WsTopics.Plinko, It.IsAny<string>()))
                .Callback<string, string>((_, p) => plinkoDropPayload = p)
                .Returns(Task.CompletedTask);

            await plinkoSut.TriggerPlinkoDropAsync(new CommandEventArgs
            {
                Name = "viewer2",
                DisplayName = "Viewer2",
                UserId = "67890",
                Color = "#FF69B4"
            });
            Assert.NotNull(plinkoDropPayload);
            Assert.Contains("\"userColor\":\"#FF69B4\"", plinkoDropPayload);
            Assert.Contains("\"color\":\"#FF69B4\"", plinkoDropPayload);

            // Test fallback/assigned color when chatter has no explicit Twitch color
            parachuteDropPayload = null;
            await parachuteSut.TriggerParachuteDropAsync(new CommandEventArgs
            {
                Name = "viewer3",
                DisplayName = "Viewer3",
                UserId = "99999",
                Color = ""
            });
            Assert.NotNull(parachuteDropPayload);
            using var doc = System.Text.Json.JsonDocument.Parse(parachuteDropPayload);
            var userColor = doc.RootElement.GetProperty("userColor").GetString();
            Assert.False(string.IsNullOrWhiteSpace(userColor));
            Assert.StartsWith("#", userColor);
        }

        [Fact]
        public void EnsureStockThemes_ConvertsRecognizedLegacySevenBucketLayout_AndPreservesCustomLayouts()
        {
            // Case 1: Recognized complete legacy 7-bucket layout is converted
            var settingsWithLegacy = new DropGamesSettings
            {
                Plinko = new PlinkoGameSettings
                {
                    Buckets =
                    [
                        new PlinkoBucket { Label = "MISS", Score = 0, IsWin = false, Color = "#64748B", Weight = 25 },
                        new PlinkoBucket { Label = "50 PTS", Score = 50, IsWin = true, Color = "#3B82F6", Weight = 20 },
                        new PlinkoBucket { Label = "100 PTS", Score = 100, IsWin = true, Color = "#10B981", Weight = 12 },
                        new PlinkoBucket { Label = "JACKPOT", Score = 500, IsWin = true, Color = "#F59E0B", Weight = 6 },
                        new PlinkoBucket { Label = "100 PTS", Score = 100, IsWin = true, Color = "#10B981", Weight = 12 },
                        new PlinkoBucket { Label = "50 PTS", Score = 50, IsWin = true, Color = "#3B82F6", Weight = 20 },
                        new PlinkoBucket { Label = "MISS", Score = 0, IsWin = false, Color = "#64748B", Weight = 25 }
                    ]
                }
            };
            DropGamesSettingsService.EnsureStockThemes(settingsWithLegacy);
            Assert.Equal(5, settingsWithLegacy.Plinko.Buckets.Count);
            Assert.Equal("WINNER", settingsWithLegacy.Plinko.Buckets[2].Label);

            // Case 2: Custom 7-bucket layout with customized scores/labels is preserved
            var settingsWithCustom7 = new DropGamesSettings
            {
                Plinko = new PlinkoGameSettings
                {
                    Buckets =
                    [
                        new PlinkoBucket { Label = "TRY AGAIN", Score = 0, IsWin = false, Color = "#64748B", Weight = 25 },
                        new PlinkoBucket { Label = "50 PTS", Score = 50, IsWin = true, Color = "#3B82F6", Weight = 20 },
                        new PlinkoBucket { Label = "100 PTS", Score = 100, IsWin = true, Color = "#10B981", Weight = 12 },
                        new PlinkoBucket { Label = "JACKPOT", Score = 1000, IsWin = true, Color = "#F59E0B", Weight = 6 },
                        new PlinkoBucket { Label = "100 PTS", Score = 100, IsWin = true, Color = "#10B981", Weight = 12 },
                        new PlinkoBucket { Label = "50 PTS", Score = 50, IsWin = true, Color = "#3B82F6", Weight = 20 },
                        new PlinkoBucket { Label = "TRY AGAIN", Score = 0, IsWin = false, Color = "#64748B", Weight = 25 }
                    ]
                }
            };
            DropGamesSettingsService.EnsureStockThemes(settingsWithCustom7);
            Assert.Equal(7, settingsWithCustom7.Plinko.Buckets.Count);
            Assert.Equal("TRY AGAIN", settingsWithCustom7.Plinko.Buckets[0].Label);
            Assert.Equal(1000, settingsWithCustom7.Plinko.Buckets[3].Score);

            // Case 3: Custom 14-bucket layout is preserved
            var settingsWith14 = new DropGamesSettings
            {
                Plinko = new PlinkoGameSettings
                {
                    Buckets = Enumerable.Range(0, 14).Select(i => new PlinkoBucket
                    {
                        Label = i == 0 ? "MISS" : (i == 1 ? "50 PTS" : (i == 3 ? "JACKPOT" : $"B{i}")),
                        Score = i * 10,
                        IsWin = i % 2 == 0,
                        Weight = 10
                    }).ToList()
                }
            };
            DropGamesSettingsService.EnsureStockThemes(settingsWith14);
            Assert.Equal(14, settingsWith14.Plinko.Buckets.Count);
        }

        [Fact]
        public async Task DropServices_StopAsync_ClearsSessionActiveInRegistry()
        {
            var registry = CreateDropRegistry();
            var parachuteSut = CreateParachuteService(dropRegistry: registry);
            var plinkoSut = CreatePlinkoService(dropRegistry: registry);

            registry.SetSessionActive(DropGameType.Parachute, true);
            Assert.True(registry.IsSessionActive(DropGameType.Parachute));
            await parachuteSut.StopAsync(CancellationToken.None);
            Assert.False(registry.IsSessionActive(DropGameType.Parachute));

            registry.SetSessionActive(DropGameType.Plinko, true);
            Assert.True(registry.IsSessionActive(DropGameType.Plinko));
            await plinkoSut.StopAsync(CancellationToken.None);
            Assert.False(registry.IsSessionActive(DropGameType.Plinko));
        }

        [Fact]
        public async Task DropServices_StopSessionAsync_TriggersEndEvents()
        {
            var registry = CreateDropRegistry();
            var parachuteSut = CreateParachuteService(dropRegistry: registry);
            var plinkoSut = CreatePlinkoService(dropRegistry: registry);

            string? firedParachuteCommand = null;
            string? firedParachuteEvent = null;
            Dictionary<string, string>? firedParachuteVars = null;

            CommandEventArgs? firedParachuteArgs = null;
            _defaultCommandTriggerService.Setup(x => x.TriggerDefaultCommandEventAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CommandEventArgs>(),
                It.IsAny<Dictionary<string, string>>()))
                .Callback<string, string, CommandEventArgs, Dictionary<string, string>?>((cmd, evt, args, vars) =>
                {
                    if (cmd == "drop")
                    {
                        firedParachuteCommand = cmd;
                        firedParachuteEvent = evt;
                        firedParachuteArgs = args;
                        firedParachuteVars = vars;
                    }
                })
                .Returns(Task.CompletedTask);

            await parachuteSut.StartSessionAsync(60, triggeredBy: "ModeratorUser");
            await parachuteSut.StopSessionAsync("ModeratorUser");

            Assert.Equal("drop", firedParachuteCommand);
            Assert.Equal(DefaultCommandEventTypes.ParachuteEnd, firedParachuteEvent);
            Assert.NotNull(firedParachuteArgs);
            Assert.Equal("drop", firedParachuteArgs.Command);
            Assert.NotNull(firedParachuteVars);
            Assert.Equal("ModeratorUser", firedParachuteVars["User"]);
            Assert.Equal("ParachuteDrop", firedParachuteVars["Game"]);
            Assert.Equal("60", firedParachuteVars["Duration"]);
            Assert.True(firedParachuteVars.ContainsKey("TotalDrops"));

            string? firedPlinkoCommand = null;
            string? firedPlinkoEvent = null;
            CommandEventArgs? firedPlinkoArgs = null;
            Dictionary<string, string>? firedPlinkoVars = null;

            _defaultCommandTriggerService.Setup(x => x.TriggerDefaultCommandEventAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CommandEventArgs>(),
                It.IsAny<Dictionary<string, string>>()))
                .Callback<string, string, CommandEventArgs, Dictionary<string, string>?>((cmd, evt, args, vars) =>
                {
                    if (cmd == "plinko")
                    {
                        firedPlinkoCommand = cmd;
                        firedPlinkoEvent = evt;
                        firedPlinkoArgs = args;
                        firedPlinkoVars = vars;
                    }
                })
                .Returns(Task.CompletedTask);

            await plinkoSut.StartSessionAsync(45, triggeredBy: "BroadcasterUser");
            await plinkoSut.StopSessionAsync("BroadcasterUser");

            Assert.Equal("plinko", firedPlinkoCommand);
            Assert.Equal(DefaultCommandEventTypes.PlinkoEnd, firedPlinkoEvent);
            Assert.NotNull(firedPlinkoArgs);
            Assert.Equal("plinko", firedPlinkoArgs.Command);
            Assert.NotNull(firedPlinkoVars);
            Assert.Equal("BroadcasterUser", firedPlinkoVars["User"]);
            Assert.Equal("PlinkoBounce", firedPlinkoVars["Game"]);
            Assert.Equal("45", firedPlinkoVars["Duration"]);
            Assert.True(firedPlinkoVars.ContainsKey("TotalDrops"));
        }

        [Fact]
        public void TriggerCatalog_ContainsParachuteAndPlinkoEndTriggers()
        {
            var parachuteEnd = TriggerCatalog.AllTriggers.FirstOrDefault(t => t.Id == "parachute-end");
            Assert.NotNull(parachuteEnd);
            Assert.Equal("minigames", parachuteEnd.CategoryId);
            Assert.Equal("drop", parachuteEnd.DefaultCommandName);
            Assert.Equal(DefaultCommandEventTypes.ParachuteEnd, parachuteEnd.DefaultCommandEventType);

            var plinkoEnd = TriggerCatalog.AllTriggers.FirstOrDefault(t => t.Id == "plinko-end");
            Assert.NotNull(plinkoEnd);
            Assert.Equal("minigames", plinkoEnd.CategoryId);
            Assert.Equal("plinko", plinkoEnd.DefaultCommandName);
            Assert.Equal(DefaultCommandEventTypes.PlinkoEnd, plinkoEnd.DefaultCommandEventType);
        }

        [Fact]
        public async Task StartSessionAsync_Performs100PercentResetAndClearsPendingDropsForGame()
        {
            var registry = CreateDropRegistry();
            var parachuteSut = CreateParachuteService(dropRegistry: registry);
            var plinkoSut = CreatePlinkoService(dropRegistry: registry);

            var paraToken = await parachuteSut.TriggerParachuteDropAsync(
                new CommandEventArgs { Name = "viewer1", DisplayName = "Viewer1", UserId = "1001" },
                emoteOrImage: null);
            var plinkoToken = await plinkoSut.TriggerPlinkoDropAsync(
                new CommandEventArgs { Name = "viewer2", DisplayName = "Viewer2", UserId = "1002" },
                emoteOrImage: null);

            Assert.NotNull(registry.GetPendingDrop(paraToken));
            Assert.NotNull(registry.GetPendingDrop(plinkoToken));

            // Starting a new Parachute session clears Parachute drops but leaves Plinko untouched
            await parachuteSut.StartSessionAsync(60, triggeredBy: "Broadcaster");
            Assert.Null(registry.GetPendingDrop(paraToken));
            Assert.NotNull(registry.GetPendingDrop(plinkoToken));

            // Starting a new Plinko session clears Plinko drops
            await plinkoSut.StartSessionAsync(60, triggeredBy: "Broadcaster");
            Assert.Null(registry.GetPendingDrop(plinkoToken));
        }

        [Fact]
        public async Task ParachuteProcessCompletionAsync_OnBullseye_FiresBothBullseyeAndWinTriggers()
        {
            var registry = CreateDropRegistry();
            var parachuteSut = CreateParachuteService(dropRegistry: registry);

            var firedEvents = new List<string>();
            _defaultCommandTriggerService.Setup(x => x.TriggerDefaultCommandEventAsync(
                "drop",
                It.IsAny<string>(),
                It.IsAny<CommandEventArgs>(),
                It.IsAny<Dictionary<string, string>>()))
                .Callback<string, string, CommandEventArgs, Dictionary<string, string>?>((cmd, evt, args, vars) =>
                {
                    firedEvents.Add(evt);
                })
                .Returns(Task.CompletedTask);

            var now = DateTime.UtcNow;
            var bullseyeRecord = new PendingDropRecord
            {
                DropToken = "bullseye-win-token",
                GameType = DropGameType.Parachute,
                Username = "winner1",
                DisplayName = "Winner1",
                UserId = "2001",
                EventArgs = new CommandEventArgs { Command = "drop", Name = "winner1", DisplayName = "Winner1", UserId = "2001" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now.AddSeconds(-3),
                EarliestCompletionUtc = now.AddSeconds(-1),
                ExpiresAtUtc = now.AddMinutes(1),
                LandingX = 0.50,
                TargetCenterX = 0.50,
                TargetHalfWidth = 0.10,
                Score = 100,
                IsHit = true,
                IsBullseye = true,
                IsWin = true
            };

            await parachuteSut.ProcessCompletionAsync(bullseyeRecord);

            Assert.Contains(DefaultCommandEventTypes.ParachuteBullseye, firedEvents);
            Assert.Contains(DefaultCommandEventTypes.ParachuteWin, firedEvents);
        }

        [Fact]
        public async Task DropServices_RespectConfigurableGravityBouncinessAndMass()
        {
            var registry = CreateDropRegistry();
            var settingsService = CreateSettingsService();

            var fastSettings = await settingsService.GetSettingsAsync();
            fastSettings.Parachute.Gravity = 2.5;
            fastSettings.Parachute.Bounciness = 0.85;
            fastSettings.Parachute.Mass = 1.5;
            fastSettings.Plinko.Gravity = 2.2;
            fastSettings.Plinko.Bounciness = 0.9;
            fastSettings.Plinko.Mass = 1.8;
            await settingsService.SaveSettingsAsync(fastSettings);

            var parachuteSut = CreateParachuteService(settingsService: settingsService, dropRegistry: registry);
            var plinkoSut = CreatePlinkoService(settingsService: settingsService, dropRegistry: registry);

            var fastParaToken = await parachuteSut.TriggerParachuteDropAsync(
                new CommandEventArgs { Name = "fast1", DisplayName = "Fast1", UserId = "3001" },
                emoteOrImage: null,
                forcedStartX: 0.5,
                forcedWindAccelX: 0.0);
            var fastParaRecord = registry.GetPendingDrop(fastParaToken);
            Assert.NotNull(fastParaRecord);
            Assert.Equal(2.5, fastParaRecord.Gravity);
            Assert.Equal(0.85, fastParaRecord.Bounciness);
            Assert.Equal(1.5, fastParaRecord.Mass);

            var fastPlinkoToken = await plinkoSut.TriggerPlinkoDropAsync(
                new CommandEventArgs { Name = "fast2", DisplayName = "Fast2", UserId = "3002" },
                emoteOrImage: null);
            var fastPlinkoRecord = registry.GetPendingDrop(fastPlinkoToken);
            Assert.NotNull(fastPlinkoRecord);
            Assert.Equal(2.2, fastPlinkoRecord.Gravity);
            Assert.Equal(0.9, fastPlinkoRecord.Bounciness);
            Assert.Equal(1.8, fastPlinkoRecord.Mass);

            var slowSettings = await settingsService.GetSettingsAsync();
            slowSettings.Parachute.Gravity = 0.4;
            slowSettings.Parachute.Bounciness = 0.3;
            slowSettings.Parachute.Mass = 0.6;
            slowSettings.Plinko.Gravity = 0.5;
            slowSettings.Plinko.Bounciness = 0.3;
            slowSettings.Plinko.Mass = 0.5;
            await settingsService.SaveSettingsAsync(slowSettings);

            var slowParaToken = await parachuteSut.TriggerParachuteDropAsync(
                new CommandEventArgs { Name = "slow1", DisplayName = "Slow1", UserId = "3003" },
                emoteOrImage: null,
                forcedStartX: 0.5,
                forcedWindAccelX: 0.0);
            var slowParaRecord = registry.GetPendingDrop(slowParaToken);
            Assert.NotNull(slowParaRecord);
            Assert.True(fastParaRecord.TotalTicks < slowParaRecord.TotalTicks);

            var slowPlinkoToken = await plinkoSut.TriggerPlinkoDropAsync(
                new CommandEventArgs { Name = "slow2", DisplayName = "Slow2", UserId = "3004" },
                emoteOrImage: null);
            var slowPlinkoRecord = registry.GetPendingDrop(slowPlinkoToken);
            Assert.NotNull(slowPlinkoRecord);
            Assert.True(fastPlinkoRecord.TotalTicks < slowPlinkoRecord.TotalTicks);
        }
    }
}

