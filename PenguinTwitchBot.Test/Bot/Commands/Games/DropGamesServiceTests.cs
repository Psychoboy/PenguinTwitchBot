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
using PenguinTwitchBot.Bot.Commands.Games.Balloon;
using PenguinTwitchBot.Bot.Commands.Games.Curling;
using PenguinTwitchBot.Bot.Commands.Games.Golf;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Models.Chat;
using PenguinTwitchBot.Bot.Models.Commands;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Bot.Overlay;
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

        [Fact]
        public void DropGamesSettings_DefaultsEveryGameToDisabledIncludingLegacyJson()
        {
            var defaults = new DropGamesSettings();
            var legacyJson = System.Text.Json.JsonSerializer.Deserialize<DropGamesSettings>("{}")!;

            Assert.False(defaults.Parachute.GameEnabled);
            Assert.False(defaults.Plinko.GameEnabled);
            Assert.False(defaults.Balloon.GameEnabled);
            Assert.False(defaults.Curling.GameEnabled);
            Assert.False(defaults.Golf.GameEnabled);
            Assert.False(legacyJson.Parachute.GameEnabled);
            Assert.False(legacyJson.Plinko.GameEnabled);
            Assert.False(legacyJson.Balloon.GameEnabled);
            Assert.False(legacyJson.Curling.GameEnabled);
            Assert.False(legacyJson.Golf.GameEnabled);
        }

        [Fact]
        public async Task DisabledDropGames_RejectNewSessionsAndIdleGameplayActions()
        {
            var settingsService = new Mock<IDropGamesSettingsService>();
            settingsService.Setup(service => service.GetSettingsAsync()).ReturnsAsync(new DropGamesSettings());
            var args = new CommandEventArgs { Name = "viewer", DisplayName = "Viewer", UserId = "viewer-id" };

            var parachute = CreateParachuteService(settingsService.Object);
            var plinko = CreatePlinkoService(settingsService.Object);
            var balloon = CreateBalloonService(settingsService: settingsService.Object);
            var curling = CreateCurlingService(CreateDropRegistry(), settingsService.Object);
            var golf = CreateGolfService(CreateDropRegistry(), settingsService.Object);

            await Assert.ThrowsAsync<InvalidOperationException>(() => parachute.StartSessionAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => plinko.StartSessionAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => balloon.StartSessionAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => curling.StartSessionAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => golf.StartSessionAsync());

            Assert.Empty(await parachute.TriggerParachuteDropAsync(args));
            Assert.Empty(await plinko.TriggerPlinkoDropAsync(args));
            Assert.Empty(await balloon.TriggerBalloonAscentAsync(args));
            Assert.Empty(await curling.TriggerCurlingSlideAsync(args));
            Assert.Empty(await golf.TriggerGolfShotAsync(args));
        }

        [Fact]
        public async Task DisablingDropGames_ClearsQueuedStartsAndKeepsActiveSessions()
        {
            var settings = new DropGamesSettings();
            settings.Parachute.GameEnabled = true;
            settings.Plinko.GameEnabled = true;
            settings.Balloon.GameEnabled = true;
            settings.Curling.GameEnabled = true;
            settings.Golf.GameEnabled = true;
            var settingsService = new Mock<IDropGamesSettingsService>();
            settingsService.Setup(service => service.GetSettingsAsync()).ReturnsAsync(settings);

            var parachute = CreateParachuteService(settingsService.Object);
            var plinko = CreatePlinkoService(settingsService.Object);
            var balloon = CreateBalloonService(settingsService: settingsService.Object);
            var curling = CreateCurlingService(CreateDropRegistry(), settingsService.Object);
            var golf = CreateGolfService(CreateDropRegistry(), settingsService.Object);
            var startSessions = new Func<Task<DropGameSessionDto>>[]
            {
                () => parachute.StartSessionAsync(), () => plinko.StartSessionAsync(), () => balloon.StartSessionAsync(),
                () => curling.StartSessionAsync(), () => golf.StartSessionAsync()
            };
            var queuedCounts = new Func<int>[]
            {
                parachute.GetQueuedSessionCount, plinko.GetQueuedSessionCount, balloon.GetQueuedSessionCount,
                curling.GetQueuedSessionCount, golf.GetQueuedSessionCount
            };
            var clearQueues = new Func<Task>[]
            {
                parachute.ClearSessionQueueAsync, plinko.ClearSessionQueueAsync, balloon.ClearSessionQueueAsync,
                curling.ClearSessionQueueAsync, golf.ClearSessionQueueAsync
            };
            var currentSessions = new Func<DropGameSessionDto?>[]
            {
                parachute.GetCurrentSession, plinko.GetCurrentSession, balloon.GetCurrentSession,
                curling.GetCurrentSession, golf.GetCurrentSession
            };

            for (var index = 0; index < startSessions.Length; index++)
            {
                Assert.True((await startSessions[index]()).IsActive);
                Assert.True((await startSessions[index]()).IsQueued);
                Assert.Equal(1, queuedCounts[index]());
                await clearQueues[index]();
                Assert.Equal(0, queuedCounts[index]());
                Assert.True(currentSessions[index]()?.IsActive);
            }
        }

        private IDropGamesSettingsService CreateSettingsService()
        {
            return new EnabledDropGamesSettingsService(
                new DropGamesSettingsService(_scopeFactory.Object, NullLogger<DropGamesSettingsService>.Instance));
        }

        private sealed class EnabledDropGamesSettingsService(IDropGamesSettingsService inner) : IDropGamesSettingsService
        {
            public async Task<DropGamesSettings> GetSettingsAsync()
            {
                var settings = await inner.GetSettingsAsync();
                settings.Parachute.GameEnabled = true;
                settings.Plinko.GameEnabled = true;
                settings.Balloon.GameEnabled = true;
                settings.Curling.GameEnabled = true;
                settings.Golf.GameEnabled = true;
                return settings;
            }

            public Task SaveSettingsAsync(DropGamesSettings settings) => inner.SaveSettingsAsync(settings);
        }

        private IOverlaySecurityService CreateOverlaySecurityService()
        {
            return new OverlaySecurityService(_scopeFactory.Object, NullLogger<OverlaySecurityService>.Instance);
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

        private BalloonAscentService CreateBalloonService(
            IDropRegistry? dropRegistry = null,
            IDropGamesSettingsService? settingsService = null)
        {
            return new BalloonAscentService(
                _serviceBackbone.Object,
                _commandHandler.Object,
                _defaultCommandTriggerService.Object,
                _scopeFactory.Object,
                _emoteService.Object,
                _webSocketMessenger.Object,
                _dispatcher.Object,
                settingsService ?? CreateSettingsService(),
                dropRegistry ?? CreateDropRegistry(),
                NullLogger<BalloonAscentService>.Instance);
        }

        private CurlingGameService CreateCurlingService(
            IDropRegistry? dropRegistry = null,
            IDropGamesSettingsService? settingsService = null)
        {
            return new CurlingGameService(
                _serviceBackbone.Object,
                _commandHandler.Object,
                _defaultCommandTriggerService.Object,
                _scopeFactory.Object,
                _emoteService.Object,
                _webSocketMessenger.Object,
                _dispatcher.Object,
                settingsService ?? CreateSettingsService(),
                dropRegistry ?? CreateDropRegistry(),
                NullLogger<CurlingGameService>.Instance);
        }

        private GolfGameService CreateGolfService(
            IDropRegistry? dropRegistry = null,
            IDropGamesSettingsService? settingsService = null)
        {
            return new GolfGameService(
                _serviceBackbone.Object,
                _commandHandler.Object,
                _defaultCommandTriggerService.Object,
                _scopeFactory.Object,
                _emoteService.Object,
                _webSocketMessenger.Object,
                _dispatcher.Object,
                settingsService ?? CreateSettingsService(),
                dropRegistry ?? CreateDropRegistry(),
                NullLogger<GolfGameService>.Instance);
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
                    if (topic == WsTopics.Plinko)
                    {
                        capturedTopic = topic;
                        capturedJson = json;
                    }
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
            Assert.All(spawned, d => Assert.StartsWith("drp_", d.Id));

            // Verify parachute_droplets message with droplets was queued on WsTopics.Parachute
            _webSocketMessenger.Verify(
                x => x.AddToQueue(
                    WsTopics.Parachute,
                    It.Is<string>(json =>
                        json.Contains("parachute_droplets") &&
                        json.Contains("droplets") &&
                        json.Contains("ChaosViewer"))),
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
        public async Task CompleteDropAsync_EnforcesTamperProofTokenSecurity_SingleUse_AndFiresOutcomeTriggers()
        {
            var settingsService = CreateSettingsService();
            var registry = CreateDropRegistry();
            var sut = CreateParachuteService(settingsService, registry);

            // 1. Unknown/forged token is rejected
            var forgedResult = registry.TryConsumeDrop("0000000000000000000000000000000000000000000000000000000000000000", out _, out _);
            Assert.False(forgedResult);

            // 2. Rapid drop completion (e.g. from rocket hit or free-fall) is accepted immediately
            var rapidToken = await sut.TriggerParachuteDropAsync(
                new CommandEventArgs { Name = "rapiduser", DisplayName = "RapidUser", UserId = "3002" },
                forcedStartX: 0.50,
                forcedWindAccelX: 0.0,
                skipMinimumDurationCheck: false);

            var rapidResult = registry.TryConsumeDrop(rapidToken, out var rapidRecord, out _);
            Assert.True(rapidResult);
            Assert.NotNull(rapidRecord);

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

            // The outcome is dispatched through the canonical default-command trigger path.
            _defaultCommandTriggerService.Verify(
                x => x.TriggerDefaultCommandEventAsync(
                    "drop",
                    DefaultCommandEventTypes.ParachuteBullseye,
                    It.Is<CommandEventArgs>(e => e.Name == "winner"),
                    It.Is<Dictionary<string, string>>(v => v["Score"] == "100" && v["IsBullseye"] == "true")),
                Times.Once);

            // 4. Replaying the same token a second time is rejected (atomic single-use)
            var replayResult = registry.TryConsumeDrop(validToken, out _, out _);
            Assert.False(replayResult);
        }

        [Fact]
        public async Task TriggerPlinkoDropAsync_SelectsWinningBucket_AndCompletesSecurely()
        {
            var settingsService = CreateSettingsService();
            var registry = CreateDropRegistry();
            var sut = CreatePlinkoService(settingsService, registry);

            var settings = await settingsService.GetSettingsAsync();

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
            Assert.Contains(WidgetRegistry.All, w => w.Type == "starship-status" && w.SourcePath == "/starship-status.html");
            Assert.Contains(WidgetRegistry.All, w => w.Type == "starship-event" && w.SourcePath == "/starship-event.html");
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

            var securityService = CreateOverlaySecurityService();
            var validKey = await securityService.GetOverlayKeyAsync();

            var handler = new DropGameCompleteHandler(
                dropRegistry,
                parachuteServiceMock.Object,
                plinkoServiceMock.Object,
                settingsService,
                securityService,
                coordinatorMock.Object);

            // Viewer bounced within tolerance (0.50 vs 0.42 = 0.08 <= 0.15) and landed dead center!
            var notification = new DropGameCompleteNotification("para-complete-token", landingX: 0.50, overlayKey: validKey);
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
            var securityService = CreateOverlaySecurityService();
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

            var validKey = await securityService.GetOverlayKeyAsync();

            var handler = new DropGameCompleteHandler(
                dropRegistry,
                parachuteServiceMock.Object,
                plinkoServiceMock.Object,
                settingsService,
                securityService,
                coordinatorMock.Object);

            // Client reports 0.50 after a big droplet/mid-air bounce from 0.20 -> outcome is recalculated as a win
            var notification = new DropGameCompleteNotification("para-large-deflection-token", landingX: 0.50, overlayKey: validKey);
            await handler.Handle(notification, CancellationToken.None);

            parachuteServiceMock.Verify(s => s.ProcessCompletionAsync(It.Is<PendingDropRecord>(r =>
                r.DropToken == "para-large-deflection-token" &&
                r.LandingX == 0.50 &&
                r.Score == 100 &&
                r.IsBullseye &&
                r.IsWin)), Times.Once);
        }

        [Fact]
        public async Task DropGameCompleteHandler_Parachute_UnauthorizedOrMissingKey_IgnoresReportedLandingX()
        {
            var dropRegistry = CreateDropRegistry();
            var settingsService = CreateSettingsService();
            var securityService = CreateOverlaySecurityService();
            var parachuteServiceMock = new Mock<IParachuteDropService>();
            var plinkoServiceMock = new Mock<IPlinkoDropService>();
            var coordinatorMock = new Mock<IFeatureRuntimeCoordinator>();
            coordinatorMock.Setup(c => c.IsEnabled(FeatureKeys.DropGames)).Returns(true);

            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = "para-unauth-token",
                GameType = DropGameType.Parachute,
                Username = "testuser",
                DisplayName = "TestUser",
                UserId = "user-1",
                EventArgs = new CommandEventArgs { Command = "drop", Name = "testuser", DisplayName = "TestUser", UserId = "user-1" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now.AddSeconds(-5),
                EarliestCompletionUtc = now.AddSeconds(-1),
                ExpiresAtUtc = now.AddMinutes(1),
                StartX = 0.10,
                StartY = 0.06,
                LandingY = 0.88,
                TargetCenterX = 0.50,
                TargetHalfWidth = 0.10,
                LandingX = 0.10, // Uncollided trajectory misses
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
                securityService,
                coordinatorMock.Object);

            // Attacker sends forged landingX = 0.50 with invalid key
            var notification = new DropGameCompleteNotification("para-unauth-token", landingX: 0.50, overlayKey: "wrong-fake-key");
            await handler.Handle(notification, CancellationToken.None);

            // Verified: unauthorized completion returns early with no fallback, never processing completion
            parachuteServiceMock.Verify(s => s.ProcessCompletionAsync(It.IsAny<PendingDropRecord>()), Times.Never);
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

            var securityService = CreateOverlaySecurityService();
            var validKey = await securityService.GetOverlayKeyAsync();

            var handler = new DropGameCompleteHandler(
                dropRegistry,
                parachuteServiceMock.Object,
                plinkoServiceMock.Object,
                settingsService,
                securityService,
                coordinatorMock.Object);

            // Viewer bumped into bucket 2
            var notification = new DropGameCompleteNotification("plinko-complete-token", winningBucketIndex: 2, overlayKey: validKey);
            await handler.Handle(notification, CancellationToken.None);

            plinkoServiceMock.Verify(s => s.ProcessCompletionAsync(It.Is<PendingDropRecord>(r =>
                r.DropToken == "plinko-complete-token" &&
                r.BucketIndex == 2 &&
                r.BucketLabel == "WINNER" &&
                r.Score == 0 &&
                r.IsWin)), Times.Once);
        }

        [Fact]
        public async Task DropGameCompleteHandler_Plinko_ForcedOutcome_PreservesRecordedOutcome()
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
                DropToken = "plinko-forced-token",
                GameType = DropGameType.Plinko,
                Username = "testuser",
                DisplayName = "TestUser",
                UserId = "user-1",
                EventArgs = new CommandEventArgs { Command = "plinko", Name = "testuser", DisplayName = "TestUser", UserId = "user-1" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now.AddSeconds(-5),
                EarliestCompletionUtc = now.AddSeconds(-1),
                ExpiresAtUtc = now.AddMinutes(1),
                StartX = 0.10,
                StartY = 0.04,
                LandingY = 0.88,
                BucketIndex = 0,
                BucketLabel = "MISS",
                Score = 0,
                IsWin = false,
                ForcedOutcome = true
            };

            dropRegistry.RegisterDrop(record);

            var securityService = CreateOverlaySecurityService();
            var validKey = await securityService.GetOverlayKeyAsync();

            var handler = new DropGameCompleteHandler(
                dropRegistry,
                parachuteServiceMock.Object,
                plinkoServiceMock.Object,
                settingsService,
                securityService,
                coordinatorMock.Object);

            // Client attempts to report bucket 2, but drop was forced to 0
            var notification = new DropGameCompleteNotification("plinko-forced-token", winningBucketIndex: 2, overlayKey: validKey);
            await handler.Handle(notification, CancellationToken.None);

            plinkoServiceMock.Verify(s => s.ProcessCompletionAsync(It.Is<PendingDropRecord>(r =>
                r.DropToken == "plinko-forced-token" &&
                r.BucketIndex == 0 &&
                r.BucketLabel == "MISS" &&
                !r.IsWin)), Times.Once);
        }

        [Fact]
        public async Task DropGameCompleteHandler_Plinko_UnresolvedOutcome_SkipsProcessingAndAcknowledges()
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
                DropToken = "plinko-unresolved-token",
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
                BucketIndex = -1,
                BucketLabel = string.Empty,
                Score = 0,
                IsWin = false
            };

            dropRegistry.RegisterDrop(record);

            var securityService = CreateOverlaySecurityService();
            var validKey = await securityService.GetOverlayKeyAsync();

            var handler = new DropGameCompleteHandler(
                dropRegistry,
                parachuteServiceMock.Object,
                plinkoServiceMock.Object,
                settingsService,
                securityService,
                coordinatorMock.Object);

            // Completion arrives without a reported winning bucket
            var notification = new DropGameCompleteNotification("plinko-unresolved-token", winningBucketIndex: null, overlayKey: validKey);
            await handler.Handle(notification, CancellationToken.None);

            // Completion must be skipped so negative bucket index does not default to bucket 0
            plinkoServiceMock.Verify(s => s.ProcessCompletionAsync(It.IsAny<PendingDropRecord>()), Times.Never);
            Assert.Null(dropRegistry.GetPendingDrop("plinko-unresolved-token"));
        }

        [Fact]
        public void PlinkoSimulationEngine_PickWeightedBucketIndex_BiasedTowardsHigherWeights()
        {
            Assert.Equal(0, PlinkoSimulationEngine.PickWeightedBucketIndex(null));
            Assert.Equal(0, PlinkoSimulationEngine.PickWeightedBucketIndex([]));

            var buckets = new List<PlinkoBucket>
            {
                new() { Label = "0", Weight = 1 },
                new() { Label = "1", Weight = 1 },
                new() { Label = "2", Weight = 1000 },
                new() { Label = "3", Weight = 1 }
            };

            var counts = new int[4];
            for (var i = 0; i < 200; i++)
            {
                var idx = PlinkoSimulationEngine.PickWeightedBucketIndex(buckets);
                counts[idx]++;
            }

            // Bucket 2 has > 99% of total weight, so it should be picked in the vast majority of iterations
            Assert.True(counts[2] >= 180, $"Bucket 2 should have been picked overwhelmingly, got {counts[2]} out of 200");
        }

        [Fact]
        public async Task DropGameCompleteHandler_Plinko_UnauthorizedOrMissingKey_IgnoresReportedBucketIndex()
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
                DropToken = "plinko-unauth-token",
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
                BucketLabel = "MISS",
                Score = 0,
                IsWin = false
            };

            dropRegistry.RegisterDrop(record);

            var securityService = CreateOverlaySecurityService();

            var handler = new DropGameCompleteHandler(
                dropRegistry,
                parachuteServiceMock.Object,
                plinkoServiceMock.Object,
                settingsService,
                securityService,
                coordinatorMock.Object);

            // Attacker sends winning bucket 2 without valid key
            var notification = new DropGameCompleteNotification("plinko-unauth-token", winningBucketIndex: 2, overlayKey: "bad-key");
            await handler.Handle(notification, CancellationToken.None);

            // Verified: unauthorized completion returns early with no fallback, never processing completion
            plinkoServiceMock.Verify(s => s.ProcessCompletionAsync(It.IsAny<PendingDropRecord>()), Times.Never);
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

            var securityService = CreateOverlaySecurityService();
            var validKey = await securityService.GetOverlayKeyAsync();

            var handler = new DropGameCompleteHandler(
                dropRegistry,
                parachuteServiceMock.Object,
                plinkoServiceMock.Object,
                settingsService,
                securityService,
                coordinatorMock.Object);

            // Client reports bucket 2 after bouncing across multiple buckets from 0 -> recalculated as win
            var notification = new DropGameCompleteNotification("plinko-multi-bucket-token", winningBucketIndex: 2, overlayKey: validKey);
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
        public async Task TriggerPlinkoDropAsync_RegistersPendingDropWithInitialPhysicsParameters()
        {
            var settingsService = CreateSettingsService();
            var registry = CreateDropRegistry();
            var sut = CreatePlinkoService(settingsService, registry);

            var token = await sut.TriggerPlinkoDropAsync(
                new CommandEventArgs { Name = "plinkouser", DisplayName = "PlinkoUser", UserId = "7001" });

            var record = registry.GetPendingDrop(token);
            Assert.NotNull(record);
            Assert.Equal(-1, record.BucketIndex);
            Assert.InRange(record.StartX, 0.02, 0.98);
            Assert.Equal(1.0, record.Gravity);
            Assert.Equal(0.75, record.Bounciness);
            Assert.Equal(1.0, record.Mass);

            // Verify outcome evaluation helper
            var outcome = PlinkoSimulationEngine.EvaluateBucketOutcome(record.Buckets, 2);
            Assert.True(outcome.IsWin);
            Assert.Equal(0, outcome.Score);
            Assert.Equal("WINNER", outcome.Label);
            Assert.True(outcome.IsBullseye);
        }

        [Fact]
        public async Task ProcessCompletionAsync_Parachute_DoesNotSendChatWithoutMessageSubAction()
        {
            var settingsService = CreateSettingsService();
            var registry = CreateDropRegistry();
            var sut = CreateParachuteService(settingsService, registry);

            var token = await sut.TriggerParachuteDropAsync(
                new CommandEventArgs { Name = "dropwinner", DisplayName = "DropWinner", UserId = "6001" },
                forcedStartX: 0.50,
                forcedWindAccelX: 0.0,
                skipMinimumDurationCheck: true);

            var consumed = registry.TryConsumeDrop(token, out var record, out _);
            Assert.True(consumed);
            Assert.NotNull(record);
            Assert.True(record.IsWin);

            var ex = await Record.ExceptionAsync(() => sut.ProcessCompletionAsync(record));
            Assert.Null(ex);

            _serviceBackbone.Verify(x => x.SendChatMessage(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ProcessCompletionAsync_Plinko_DoesNotSendChatWithoutMessageSubAction()
        {
            var settingsService = CreateSettingsService();
            var registry = CreateDropRegistry();
            var sut = CreatePlinkoService(settingsService, registry);

            var settings = await settingsService.GetSettingsAsync();

            var winIndex = settings.Plinko.Buckets.FindIndex(b => b.IsWin);
            var token = await sut.TriggerPlinkoDropAsync(
                new CommandEventArgs { Name = "plinkowinner", DisplayName = "PlinkoWinner", UserId = "6002" },
                forcedBucketIndex: winIndex,
                skipMinimumDurationCheck: true);

            var consumed = registry.TryConsumeDrop(token, out var record, out _);
            Assert.True(consumed);
            Assert.NotNull(record);
            Assert.True(record.IsWin);

            var ex = await Record.ExceptionAsync(() => sut.ProcessCompletionAsync(record));
            Assert.Null(ex);

            _serviceBackbone.Verify(x => x.SendChatMessage(It.IsAny<string>()), Times.Never);
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
        public async Task DropGameMediaResolver_ResolveAndPrepareThemeAsync_ResolvesEmotesAndRegistersProxyUrls()
        {
            var emoteService = new Mock<IEmoteService>();
            emoteService.Setup(e => e.GetEmotesAsync())
                .ReturnsAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sptv30k"] = "https://static-cdn.jtvnw.net/emoticons/v2/30123/default/dark/3.0",
                    ["sptvPin"] = "https://static-cdn.jtvnw.net/emoticons/v2/30456/default/dark/3.0"
                });

            var dropRegistry = CreateDropRegistry();
            var settings = new DropGamesSettings();
            var customTheme = new DropGameTheme
            {
                Id = "custom_test",
                Name = "Custom Test Theme",
                PlinkoPegImageUrl = "sptv30k",
                PlinkoWinningSlotImageUrl = "/media/custom_trophy.png",
                PlinkoDividerImageUrl = "images/custom_divider.png",
                PlinkoBoardBackgroundImageUrl = "https://example.com/bg.png",
                GolfFlagPinImageUrl = "sptvPin",
                TargetPlatformImageUrl = "sptv30k"
            };
            settings.Themes.Add(customTheme);

            var resolved = await DropGameMediaResolver.ResolveAndPrepareThemeAsync(
                settings,
                "custom_test",
                "spring",
                emoteService.Object,
                dropRegistry);

            Assert.Equal("https://static-cdn.jtvnw.net/emoticons/v2/30123/default/dark/3.0", resolved.PlinkoPegImageUrl);
            Assert.Equal("/media/custom_trophy.png", resolved.PlinkoWinningSlotImageUrl);
            Assert.Equal("/images/custom_divider.png", resolved.PlinkoDividerImageUrl);
            Assert.Equal("https://example.com/bg.png", resolved.PlinkoBoardBackgroundImageUrl);
            Assert.Equal("https://static-cdn.jtvnw.net/emoticons/v2/30456/default/dark/3.0", resolved.GolfFlagPinImageUrl);
            Assert.Equal("https://static-cdn.jtvnw.net/emoticons/v2/30123/default/dark/3.0", resolved.TargetPlatformImageUrl);

            // Raw theme in settings remains unchanged
            Assert.Equal("sptv30k", customTheme.PlinkoPegImageUrl);

            // All URLs registered in DropRegistry
            Assert.True(dropRegistry.IsProxyUrlAllowed("https://static-cdn.jtvnw.net/emoticons/v2/30123/default/dark/3.0"));
            Assert.True(dropRegistry.IsProxyUrlAllowed("/media/custom_trophy.png"));
            Assert.True(dropRegistry.IsProxyUrlAllowed("/images/custom_divider.png"));
            Assert.True(dropRegistry.IsProxyUrlAllowed("https://example.com/bg.png"));
            Assert.True(dropRegistry.IsProxyUrlAllowed("https://static-cdn.jtvnw.net/emoticons/v2/30456/default/dark/3.0"));
        }

        [Fact]
        public async Task PlinkoDropService_TriggerPlinkoDropAsync_ResolvesThemeCustomSpritesAndRegistersProxyUrls()
        {
            _emoteService.Setup(e => e.GetEmotesAsync())
                .ReturnsAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sptvPeg"] = "https://static-cdn.jtvnw.net/emoticons/v2/999/default/dark/3.0"
                });

            var dropRegistry = CreateDropRegistry();
            var settings = new DropGamesSettings();
            settings.Plinko.GameEnabled = true;
            settings.Plinko.ActiveThemeId = "custom_plinko";
            var customTheme = new DropGameTheme
            {
                Id = "custom_plinko",
                Name = "Custom Plinko Theme",
                PlinkoPegImageUrl = "sptvPeg",
                PlinkoWinningSlotImageUrl = "/media/slot.png"
            };
            settings.Themes.Add(customTheme);

            var settingsService = new Mock<IDropGamesSettingsService>();
            settingsService.Setup(s => s.GetSettingsAsync()).ReturnsAsync(settings);

            var sentMessages = new List<string>();
            _webSocketMessenger.Setup(w => w.AddToQueue(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((topic, msg) => sentMessages.Add(msg))
                .Returns(Task.CompletedTask);

            var service = CreatePlinkoService(settingsService.Object, dropRegistry);

            var args = new CommandEventArgs { Name = "testuser", DisplayName = "TestUser", UserId = "123" };
            var token = await service.TriggerPlinkoDropAsync(args);

            Assert.False(string.IsNullOrWhiteSpace(token));
            var dropMsg = sentMessages.FirstOrDefault(m => m.Contains("\"type\":\"plinko_drop\""));
            Assert.NotNull(dropMsg);
            Assert.Contains("https://static-cdn.jtvnw.net/emoticons/v2/999/default/dark/3.0", dropMsg);
            Assert.Contains("/media/slot.png", dropMsg);

            Assert.True(dropRegistry.IsProxyUrlAllowed("https://static-cdn.jtvnw.net/emoticons/v2/999/default/dark/3.0"));
            Assert.True(dropRegistry.IsProxyUrlAllowed("/media/slot.png"));
        }

        [Fact]
        public void TriggerCatalog_ContainsParachuteAndPlinkoStartTriggers()
        {
            var parachuteStart = TriggerCatalog.AllTriggers.FirstOrDefault(t => t.Id == "parachute-start");
            Assert.NotNull(parachuteStart);
            Assert.Equal("minigames", parachuteStart.CategoryId);
            Assert.Equal("drop", parachuteStart.DefaultCommandName);
            Assert.Equal(DefaultCommandEventTypes.ParachuteStart, parachuteStart.DefaultCommandEventType);

            var plinkoStart = TriggerCatalog.AllTriggers.FirstOrDefault(t => t.Id == "plinko-start");
            Assert.NotNull(plinkoStart);
            Assert.Equal("minigames", plinkoStart.CategoryId);
            Assert.Equal("plinko", plinkoStart.DefaultCommandName);
            Assert.Equal(DefaultCommandEventTypes.PlinkoStart, plinkoStart.DefaultCommandEventType);
        }

        [Fact]
        public async Task StartSessionAsync_FiresDefaultCommandStartTriggerWithVariables()
        {
            var parachuteSut = CreateParachuteService();
            var plinkoSut = CreatePlinkoService();

            string? firedParachuteCommand = null;
            string? firedParachuteEvent = null;
            CommandEventArgs? firedParachuteArgs = null;
            Dictionary<string, string>? firedParachuteVars = null;

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

            await parachuteSut.StartSessionAsync(60, triggeredBy: "StartModUser");

            Assert.Equal("drop", firedParachuteCommand);
            Assert.Equal(DefaultCommandEventTypes.ParachuteStart, firedParachuteEvent);
            Assert.NotNull(firedParachuteArgs);
            Assert.Equal("drop", firedParachuteArgs.Command);
            Assert.NotNull(firedParachuteVars);
            Assert.Equal("StartModUser", firedParachuteVars["User"]);
            Assert.Equal("ParachuteDrop", firedParachuteVars["Game"]);
            Assert.Equal("60", firedParachuteVars["Duration"]);
            Assert.True(firedParachuteVars.ContainsKey("Theme"));

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

            await plinkoSut.StartSessionAsync(45, triggeredBy: "PlinkoBroadcaster");

            Assert.Equal("plinko", firedPlinkoCommand);
            Assert.Equal(DefaultCommandEventTypes.PlinkoStart, firedPlinkoEvent);
            Assert.NotNull(firedPlinkoArgs);
            Assert.Equal("plinko", firedPlinkoArgs.Command);
            Assert.NotNull(firedPlinkoVars);
            Assert.Equal("PlinkoBroadcaster", firedPlinkoVars["User"]);
            Assert.Equal("Plinko", firedPlinkoVars["Game"]);
            Assert.Equal("45", firedPlinkoVars["Duration"]);
            Assert.True(firedPlinkoVars.ContainsKey("Theme"));
        }

        [Fact]
        public async Task CurlingAndGolfStartTriggersExposeTriggeredBy()
        {
            var settingsService = CreateSettingsService();
            var curlingService = CreateCurlingService(CreateDropRegistry(), settingsService);
            var golfService = CreateGolfService(CreateDropRegistry(), settingsService);
            var startVariables = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            _defaultCommandTriggerService.Setup(x => x.TriggerDefaultCommandEventAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CommandEventArgs>(),
                    It.IsAny<Dictionary<string, string>>()))
                .Callback<string, string, CommandEventArgs, Dictionary<string, string>?>((_, eventType, _, variables) =>
                {
                    if (variables != null && eventType is DefaultCommandEventTypes.CurlingStart or DefaultCommandEventTypes.GolfStart)
                    {
                        startVariables[eventType] = variables;
                    }
                })
                .Returns(Task.CompletedTask);

            await curlingService.StartSessionAsync(60, triggeredBy: "CurlingMod");
            await golfService.StartSessionAsync(60, triggeredBy: "GolfMod");

            Assert.Equal("CurlingMod", startVariables[DefaultCommandEventTypes.CurlingStart]["TriggeredBy"]);
            Assert.Equal("CurlingMod", startVariables[DefaultCommandEventTypes.CurlingStart]["User"]);
            Assert.Equal("GolfMod", startVariables[DefaultCommandEventTypes.GolfStart]["TriggeredBy"]);
            Assert.Equal("GolfMod", startVariables[DefaultCommandEventTypes.GolfStart]["User"]);
        }

        [Fact]
        public async Task StartSessionAsync_WhenQueued_DoesNotFireStartTriggerUntilDequeued()
        {
            var parachuteSut = CreateParachuteService();

            var startTriggerEvents = new List<string>();
            var endTriggerEvents = new List<string>();

            _defaultCommandTriggerService.Setup(x => x.TriggerDefaultCommandEventAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CommandEventArgs>(),
                It.IsAny<Dictionary<string, string>>()))
                .Callback<string, string, CommandEventArgs, Dictionary<string, string>?>((cmd, evt, args, vars) =>
                {
                    if (evt == DefaultCommandEventTypes.ParachuteStart)
                    {
                        startTriggerEvents.Add(vars?["User"] ?? "Unknown");
                    }
                    else if (evt == DefaultCommandEventTypes.ParachuteEnd)
                    {
                        endTriggerEvents.Add(vars?["User"] ?? "Unknown");
                    }
                })
                .Returns(Task.CompletedTask);

            // 1. Start active session 1
            var s1 = await parachuteSut.StartSessionAsync(60, triggeredBy: "UserFirst");
            Assert.True(s1.IsActive);
            Assert.False(s1.IsQueued);
            Assert.Single(startTriggerEvents);
            Assert.Equal("UserFirst", startTriggerEvents[0]);

            // 2. Start session 2 while session 1 is active -> should be queued
            var s2 = await parachuteSut.StartSessionAsync(30, triggeredBy: "UserSecond");
            Assert.False(s2.IsActive);
            Assert.True(s2.IsQueued);
            Assert.Equal(1, s2.QueuePosition);

            // CRITICAL: Ensure NO start trigger fired for the queued session
            Assert.Single(startTriggerEvents);

            // 3. Stop session 1 -> triggers End for session 1, then dequeues and starts session 2
            await parachuteSut.StopSessionAsync("UserFirst");

            Assert.Single(endTriggerEvents);
            Assert.Equal("UserFirst", endTriggerEvents[0]);

            // Now session 2 should have started, firing its start trigger
            Assert.Equal(2, startTriggerEvents.Count);
            Assert.Equal("UserSecond", startTriggerEvents[1]);
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
            Dictionary<string, string>? outcomeVariables = null;
            _defaultCommandTriggerService.Setup(x => x.TriggerDefaultCommandEventAsync(
                "drop",
                It.IsAny<string>(),
                It.IsAny<CommandEventArgs>(),
                It.IsAny<Dictionary<string, string>>()))
                .Callback<string, string, CommandEventArgs, Dictionary<string, string>?>((cmd, evt, args, vars) =>
                {
                    firedEvents.Add(evt);
                    outcomeVariables = vars;
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
            Assert.NotNull(outcomeVariables);
            Assert.Equal("0.0", outcomeVariables["DistanceFromCenter"]);
            Assert.Equal("0", outcomeVariables["DropletHits"]);
        }

        [Fact]
        public async Task BalloonProcessCompletionAsync_OnBullseye_FiresBothBullseyeAndWinTriggers()
        {
            var balloonSut = CreateBalloonService();
            var firedEvents = new List<string>();
            Dictionary<string, string>? outcomeVariables = null;
            _defaultCommandTriggerService.Setup(x => x.TriggerDefaultCommandEventAsync(
                    "balloon",
                    It.IsAny<string>(),
                    It.IsAny<CommandEventArgs>(),
                    It.IsAny<Dictionary<string, string>>()))
                .Callback<string, string, CommandEventArgs, Dictionary<string, string>?>((_, eventType, _, variables) =>
                {
                    firedEvents.Add(eventType);
                    outcomeVariables = variables;
                })
                .Returns(Task.CompletedTask);

            await balloonSut.ProcessCompletionAsync(new PendingDropRecord
            {
                DropToken = "balloon-bullseye-win-token",
                GameType = DropGameType.Balloon,
                Username = "winner1",
                DisplayName = "Winner1",
                UserId = "2001",
                EventArgs = new CommandEventArgs { Command = "balloon", Name = "winner1", DisplayName = "Winner1", UserId = "2001" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = DateTime.UtcNow,
                EarliestCompletionUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(1),
                Score = 100,
                IsBullseye = true,
                IsWin = true,
                IsHit = true,
                LandingX = 0.44,
                TargetCenterX = 0.50
            });

            Assert.Contains(DefaultCommandEventTypes.BalloonBullseye, firedEvents);
            Assert.Contains(DefaultCommandEventTypes.BalloonWin, firedEvents);
            Assert.DoesNotContain(DefaultCommandEventTypes.BalloonLose, firedEvents);
            Assert.NotNull(outcomeVariables);
            Assert.Equal("6.0", outcomeVariables["DistanceFromCenter"]);
        }

        [Fact]
        public async Task CurlingProcessCompletionAsync_OnBullseye_FiresBothBullseyeAndWinTriggers()
        {
            var curlingSut = CreateCurlingService();
            var firedEvents = new List<string>();
            _defaultCommandTriggerService.Setup(x => x.TriggerDefaultCommandEventAsync(
                    "curl",
                    It.IsAny<string>(),
                    It.IsAny<CommandEventArgs>(),
                    It.IsAny<Dictionary<string, string>>()))
                .Callback<string, string, CommandEventArgs, Dictionary<string, string>?>((_, eventType, _, _) => firedEvents.Add(eventType))
                .Returns(Task.CompletedTask);

            await curlingSut.ProcessCompletionAsync(new PendingDropRecord
            {
                DropToken = "curling-bullseye-win-token",
                GameType = DropGameType.Curling,
                Username = "winner1",
                DisplayName = "Winner1",
                UserId = "2001",
                EventArgs = new CommandEventArgs { Command = "curl", Name = "winner1", DisplayName = "Winner1", UserId = "2001" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = DateTime.UtcNow,
                EarliestCompletionUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(1),
                Score = 100,
                IsBullseye = true,
                IsWin = true,
                IsHit = true
            });

            Assert.Contains(DefaultCommandEventTypes.CurlingBullseye, firedEvents);
            Assert.Contains(DefaultCommandEventTypes.CurlingWin, firedEvents);
            Assert.DoesNotContain(DefaultCommandEventTypes.CurlingLose, firedEvents);
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

        [Fact]
        public async Task ParachuteDropService_FireRockets_GeneratesRocketsAndBroadcasts()
        {
            var registry = CreateDropRegistry();
            var parachuteSut = CreateParachuteService(dropRegistry: registry);

            // Drop an active parachutist to be targeted
            var dropToken = await parachuteSut.TriggerParachuteDropAsync(
                new CommandEventArgs { Name = "targetPlayer", DisplayName = "TargetPlayer", UserId = "5001" },
                emoteOrImage: null,
                forcedStartX: 0.50,
                forcedWindAccelX: 0.0);

            var rockets = await parachuteSut.FireRocketsAsync(new RocketFireRequest
            {
                Count = 3,
                Pattern = "Barrage",
                TriggeredByUser = "AdminStreamer"
            });

            Assert.NotNull(rockets);
            Assert.Equal(3, rockets.Count);
            Assert.All(rockets, r =>
            {
                Assert.False(string.IsNullOrEmpty(r.Id));
                Assert.True(r.Vy < 0); // Firing upwards
                Assert.True(r.FuseTicks > 0);
            });

            _webSocketMessenger.Verify(x => x.AddToQueue(
                WsTopics.Parachute,
                It.Is<string>(json => json.Contains("parachute_rockets"))),
                Times.AtLeastOnce());
        }

        [Fact]
        public async Task ParachuteDropService_FireRockets_TargetedWithoutTargetUser_SelectsFromActiveCandidates()
        {
            var registry = CreateDropRegistry();
            var parachuteSut = CreateParachuteService(dropRegistry: registry);

            var token1 = await parachuteSut.TriggerParachuteDropAsync(
                new CommandEventArgs { Name = "player1", DisplayName = "Player1", UserId = "5001" },
                emoteOrImage: null,
                forcedStartX: 0.30,
                forcedWindAccelX: 0.0);

            var token2 = await parachuteSut.TriggerParachuteDropAsync(
                new CommandEventArgs { Name = "player2", DisplayName = "Player2", UserId = "5002" },
                emoteOrImage: null,
                forcedStartX: 0.70,
                forcedWindAccelX: 0.0);

            var rockets = await parachuteSut.FireRocketsAsync(new RocketFireRequest
            {
                Count = 4,
                Pattern = "Targeted",
                TargetUser = null,
                TriggeredByUser = "Streamer"
            });

            Assert.NotNull(rockets);
            Assert.Equal(4, rockets.Count);
            Assert.All(rockets, r =>
            {
                Assert.True(r.IsTargeted);
                Assert.True(r.TargetDropToken == token1 || r.TargetDropToken == token2);
            });
        }

        [Theory]
        [InlineData(CrashLandingScoreMode.PreserveScore, 0.50, 0.50, 0.10, true, 100, true, true)]
        [InlineData(CrashLandingScoreMode.HalfScore, 0.50, 0.50, 0.10, true, 50, false, true)]
        [InlineData(CrashLandingScoreMode.ZeroScore, 0.50, 0.50, 0.10, true, 0, false, false)]
        [InlineData(CrashLandingScoreMode.PreserveScore, 0.55, 0.50, 0.10, true, 51, false, true)]
        [InlineData(CrashLandingScoreMode.HalfScore, 0.55, 0.50, 0.10, true, 26, false, true)]
        [InlineData(CrashLandingScoreMode.PreserveScore, 0.80, 0.50, 0.10, true, 0, false, false)] // Missed target
        public void ParachuteSimulationEngine_CalculateLandingScore_EvaluatesCrashLandingScoreModes(
            CrashLandingScoreMode scoreMode,
            double landingX,
            double targetCenterX,
            double targetHalfWidth,
            bool parachuteDestroyed,
            int expectedScore,
            bool expectedBullseye,
            bool expectedWin)
        {
            var (score, isBullseye, isWin, _) = ParachuteSimulationEngine.CalculateLandingScore(
                landingX,
                targetCenterX,
                targetHalfWidth,
                parachuteDestroyed,
                scoreMode);

            Assert.Equal(expectedScore, score);
            Assert.Equal(expectedBullseye, isBullseye);
            Assert.Equal(expectedWin, isWin);
        }

        [Fact]
        public async Task ParachuteRocketsHandler_ExecutesAndCallsFireRockets()
        {
            var parachuteMock = new Mock<IParachuteDropService>();
            parachuteMock.Setup(x => x.FireRocketsAsync(It.IsAny<RocketFireRequest>()))
                .ReturnsAsync([new RocketInstanceDto { Id = "r1" }]);

            var handler = new PenguinTwitchBot.Bot.Actions.SubActions.Handlers.ParachuteRocketsHandler(parachuteMock.Object);

            var variables = new ConcurrentDictionary<string, string>();
            variables["User"] = "TestDonor";

            var subAction = new ParachuteRocketsType
            {
                Count = 5,
                Pattern = "Barrage",
                TargetUser = "LuckyViewer",
                CanopyKillChance = 75,
                KnockbackMultiplier = 1.5,
                CrashLandingScoreMode = "HalfScore"
            };

            await handler.ExecuteAsync(subAction, variables);

            parachuteMock.Verify(x => x.FireRocketsAsync(It.Is<RocketFireRequest>(r =>
                r.Count == 5 &&
                r.Pattern == "Barrage" &&
                r.TargetUser == "LuckyViewer" &&
                r.CanopyKillChance == 75 &&
                r.KnockbackMultiplier == 1.5 &&
                r.ScoreModeOverride == CrashLandingScoreMode.HalfScore &&
                r.TriggeredByUser == "TestDonor")),
                Times.Once());
        }

        [Fact]
        public void ParachuteRocketKill_TriggerCatalogAndVariables_AreProperlyConfigured()
        {
            var triggerItem = PenguinTwitchBot.Bot.Actions.Triggers.TriggerCatalog.AllTriggers
                .FirstOrDefault(i => i.Id == "parachute-rocket-kill");

            Assert.NotNull(triggerItem);
            Assert.Equal(PenguinTwitchBot.Database.Bot.Models.Actions.Triggers.TriggerTypes.DefaultCommand, triggerItem.Type);
            Assert.Equal("drop", triggerItem.DefaultCommandName);
            Assert.Equal(PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations.DefaultCommandEventTypes.ParachuteRocketKill, triggerItem.DefaultCommandEventType);

            var resolver = new PenguinTwitchBot.Bot.Actions.Variables.Triggers.DefaultCommandTriggerVariableResolver();
            var variables = new Dictionary<string, PenguinTwitchBot.Bot.Actions.Variables.ActionVariableInfo>();
            var trigger = new PenguinTwitchBot.Database.Bot.Models.Actions.Triggers.TriggerType
            {
                Type = PenguinTwitchBot.Database.Bot.Models.Actions.Triggers.TriggerTypes.DefaultCommand,
                Name = "Parachute Rocket Kill",
                Configuration = System.Text.Json.JsonSerializer.Serialize(new PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations.DefaultCommandTriggerConfiguration
                {
                    DefaultCommandName = "drop",
                    EventType = PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations.DefaultCommandEventTypes.ParachuteRocketKill
                })
            };

            resolver.ResolveVariables(trigger, variables, "test");

            Assert.True(variables.ContainsKey("Attacker"));
            Assert.True(variables.ContainsKey("Victim"));
            Assert.True(variables.ContainsKey("ParachuteDestroyed"));
        }

        [Fact]
        public async Task ParachuteDropService_ProcessCompletion_WithParachuteDestroyed_TriggersParachuteRocketKill()
        {
            var sut = CreateParachuteService();

            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = "kill-completion-token",
                GameType = DropGameType.Parachute,
                Username = "VictimPenguin",
                DisplayName = "VictimPenguin",
                UserId = "999",
                EventArgs = new CommandEventArgs { Command = "drop", Name = "VictimPenguin", DisplayName = "VictimPenguin", UserId = "999" },
                Theme = new DropGameTheme { Name = "Arctic" },
                CreatedAtUtc = now,
                EarliestCompletionUtc = now,
                ExpiresAtUtc = now.AddMinutes(1),
                StartX = 0.50,
                TargetCenterX = 0.50,
                TargetHalfWidth = 0.10,
                LandingY = 0.90,
                ParachuteDestroyed = true,
                RocketAttacker = "RocketSniper",
                Score = 50,
                IsWin = true
            };

            await sut.ProcessCompletionAsync(record);

            _defaultCommandTriggerService.Verify(x => x.TriggerDefaultCommandEventAsync(
                "drop",
                PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations.DefaultCommandEventTypes.ParachuteRocketKill,
                record.EventArgs,
                It.Is<Dictionary<string, string>>(v =>
                    v["Attacker"] == "RocketSniper" &&
                    v["Victim"] == "VictimPenguin" &&
                    v["ParachuteDestroyed"] == "true")),
                Times.Once());
        }

        [Fact]
        public async Task DropGameHeartbeatHandler_AuthorizedHeartbeat_UpdatesLivenessAndExtendsExpiry()
        {
            var dropRegistry = CreateDropRegistry();
            var securityService = CreateOverlaySecurityService();
            var coordinatorMock = new Mock<IFeatureRuntimeCoordinator>();
            coordinatorMock.Setup(c => c.IsEnabled(FeatureKeys.DropGames)).Returns(true);

            var validKey = await securityService.GetOverlayKeyAsync();

            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = "heartbeat-token-1",
                GameType = DropGameType.Parachute,
                Username = "testuser",
                DisplayName = "TestUser",
                UserId = "user-1",
                EventArgs = new CommandEventArgs { Command = "drop", Name = "testuser", DisplayName = "TestUser", UserId = "user-1" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now.AddSeconds(-2),
                EarliestCompletionUtc = now.AddSeconds(5),
                ExpiresAtUtc = now.AddSeconds(10)
            };

            dropRegistry.RegisterDrop(record);

            var handler = new DropGameHeartbeatHandler(
                dropRegistry,
                securityService,
                coordinatorMock.Object,
                NullLogger<DropGameHeartbeatHandler>.Instance);

            var notification = new DropGameHeartbeatNotification(
                game: "parachute",
                activeTokens: ["heartbeat-token-1"],
                completingTokens: [],
                overlayKey: validKey);

            await handler.Handle(notification, CancellationToken.None);

            Assert.NotNull(record.LastHeartbeatUtc);
            Assert.Equal(0, record.MissedHeartbeatCount);
            Assert.True(record.ExpiresAtUtc >= DateTime.UtcNow.AddSeconds(14));
        }

        [Fact]
        public async Task DropGameHeartbeatHandler_MissedHeartbeatsBeyondTolerance_PrunesDroppedToken()
        {
            var dropRegistry = CreateDropRegistry();
            var securityService = CreateOverlaySecurityService();
            var coordinatorMock = new Mock<IFeatureRuntimeCoordinator>();
            coordinatorMock.Setup(c => c.IsEnabled(FeatureKeys.DropGames)).Returns(true);

            var validKey = await securityService.GetOverlayKeyAsync();

            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = "stale-drop-token",
                GameType = DropGameType.Parachute,
                Username = "testuser",
                DisplayName = "TestUser",
                UserId = "user-1",
                EventArgs = new CommandEventArgs { Command = "drop", Name = "testuser", DisplayName = "TestUser", UserId = "user-1" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now.AddSeconds(-15),
                EarliestCompletionUtc = now.AddSeconds(-5), // Already past flight time
                ExpiresAtUtc = now.AddSeconds(60)
            };

            dropRegistry.RegisterDrop(record);

            var handler = new DropGameHeartbeatHandler(
                dropRegistry,
                securityService,
                coordinatorMock.Object,
                NullLogger<DropGameHeartbeatHandler>.Instance);

            // 1st heartbeat: missing token
            var notification1 = new DropGameHeartbeatNotification("parachute", activeTokens: [], completingTokens: [], overlayKey: validKey);
            await handler.Handle(notification1, CancellationToken.None);
            Assert.Equal(1, record.MissedHeartbeatCount);
            Assert.NotNull(dropRegistry.GetPendingDrop("stale-drop-token"));

            // 2nd heartbeat: missing token
            await handler.Handle(notification1, CancellationToken.None);
            Assert.Equal(2, record.MissedHeartbeatCount);
            Assert.NotNull(dropRegistry.GetPendingDrop("stale-drop-token"));

            // 3rd heartbeat: tolerance exceeded (MaxMissedHeartbeats = 3) -> pruned
            await handler.Handle(notification1, CancellationToken.None);
            Assert.Null(dropRegistry.GetPendingDrop("stale-drop-token"));
        }

        [Fact]
        public async Task DropGameHeartbeatHandler_UnauthorizedHeartbeat_Ignored()
        {
            var dropRegistry = CreateDropRegistry();
            var securityService = CreateOverlaySecurityService();
            var coordinatorMock = new Mock<IFeatureRuntimeCoordinator>();
            coordinatorMock.Setup(c => c.IsEnabled(FeatureKeys.DropGames)).Returns(true);

            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = "unauth-drop-token",
                GameType = DropGameType.Parachute,
                Username = "testuser",
                DisplayName = "TestUser",
                UserId = "user-1",
                EventArgs = new CommandEventArgs { Command = "drop", Name = "testuser", DisplayName = "TestUser", UserId = "user-1" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now.AddSeconds(-15),
                EarliestCompletionUtc = now.AddSeconds(-5),
                ExpiresAtUtc = now.AddSeconds(60)
            };

            dropRegistry.RegisterDrop(record);

            var handler = new DropGameHeartbeatHandler(
                dropRegistry,
                securityService,
                coordinatorMock.Object,
                NullLogger<DropGameHeartbeatHandler>.Instance);

            var notification = new DropGameHeartbeatNotification("parachute", activeTokens: [], completingTokens: [], overlayKey: "bad-fake-key");
            await handler.Handle(notification, CancellationToken.None);

            // MissedHeartbeatCount remains 0 and token was not pruned
            Assert.Equal(0, record.MissedHeartbeatCount);
            Assert.NotNull(dropRegistry.GetPendingDrop("unauth-drop-token"));
        }

        [Fact]
        public async Task DropGameCompleteHandler_Balloon_Authorized_TrustsClientOutcome()
        {
            var dropRegistry = CreateDropRegistry();
            var settingsService = CreateSettingsService();
            var parachuteServiceMock = new Mock<IParachuteDropService>();
            var plinkoServiceMock = new Mock<IPlinkoDropService>();
            var balloonServiceMock = new Mock<IBalloonAscentService>();
            var coordinatorMock = new Mock<IFeatureRuntimeCoordinator>();
            coordinatorMock.Setup(c => c.IsEnabled(FeatureKeys.DropGames)).Returns(true);
            var securityService = CreateOverlaySecurityService();
            var validKey = await securityService.GetOverlayKeyAsync();

            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = "balloon-auth-token",
                GameType = DropGameType.Balloon,
                Username = "balloon_pilot",
                DisplayName = "BalloonPilot",
                UserId = "pilot-1",
                EventArgs = new CommandEventArgs { Command = "balloon", Name = "balloon_pilot", DisplayName = "BalloonPilot", UserId = "pilot-1" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now.AddSeconds(-8),
                EarliestCompletionUtc = now.AddSeconds(-2),
                ExpiresAtUtc = now.AddSeconds(60)
            };

            dropRegistry.RegisterDrop(record);

            var handler = new DropGameCompleteHandler(
                dropRegistry,
                parachuteServiceMock.Object,
                plinkoServiceMock.Object,
                settingsService,
                securityService,
                coordinatorMock.Object,
                balloonServiceMock.Object);

            var notification = new DropGameCompleteNotification(
                "balloon-auth-token",
                landingX: 0.505,
                score: 100,
                isBullseye: true,
                isWin: true,
                isHit: true,
                balloonPopped: false,
                overlayKey: validKey);

            await handler.Handle(notification, CancellationToken.None);

            balloonServiceMock.Verify(s => s.ProcessCompletionAsync(It.Is<PendingDropRecord>(r =>
                r.DropToken == "balloon-auth-token" &&
                r.Score == 100 &&
                r.IsBullseye == true &&
                r.IsWin == true &&
                r.IsHit == true &&
                r.BalloonPopped == false &&
                r.LandingX == 0.505)), Times.Once);
        }

        [Fact]
        public async Task DropGameCompleteHandler_Balloon_Unauthorized_ReturnsEarly()
        {
            var dropRegistry = CreateDropRegistry();
            var settingsService = CreateSettingsService();
            var parachuteServiceMock = new Mock<IParachuteDropService>();
            var plinkoServiceMock = new Mock<IPlinkoDropService>();
            var balloonServiceMock = new Mock<IBalloonAscentService>();
            var coordinatorMock = new Mock<IFeatureRuntimeCoordinator>();
            coordinatorMock.Setup(c => c.IsEnabled(FeatureKeys.DropGames)).Returns(true);
            var securityService = CreateOverlaySecurityService();

            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = "balloon-unauth-token",
                GameType = DropGameType.Balloon,
                Username = "balloon_attacker",
                DisplayName = "BalloonAttacker",
                UserId = "attacker-1",
                EventArgs = new CommandEventArgs { Command = "balloon", Name = "balloon_attacker", DisplayName = "BalloonAttacker", UserId = "attacker-1" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now.AddSeconds(-8),
                EarliestCompletionUtc = now.AddSeconds(-2),
                ExpiresAtUtc = now.AddSeconds(60)
            };

            dropRegistry.RegisterDrop(record);

            var handler = new DropGameCompleteHandler(
                dropRegistry,
                parachuteServiceMock.Object,
                plinkoServiceMock.Object,
                settingsService,
                securityService,
                coordinatorMock.Object,
                balloonServiceMock.Object);

            var notification = new DropGameCompleteNotification(
                "balloon-unauth-token",
                landingX: 0.50,
                score: 100,
                isBullseye: true,
                isWin: true,
                overlayKey: "invalid-key");

            await handler.Handle(notification, CancellationToken.None);

            balloonServiceMock.Verify(s => s.ProcessCompletionAsync(It.IsAny<PendingDropRecord>()), Times.Never);
        }

        [Fact]
        public async Task BalloonAscentService_SpawnHazardsAsync_ResolvesArchetypeSpecificImages()
        {
            var dropRegistry = CreateDropRegistry();
            var settingsService = CreateSettingsService();
            var settings = await settingsService.GetSettingsAsync();

            settings.Balloon.DefaultDartEmote = "dart_emote";
            settings.Balloon.DefaultAnvilEmote = "anvil_emote";
            settings.Balloon.DefaultNudgeEmote = "nudge_emote";
            settings.Balloon.DefaultWindGustEmote = "wind_emote";

            var defaultTheme = settings.Themes.First(t => t.Id == settings.Balloon.ActiveThemeId);
            defaultTheme.BalloonDartImageUrl = "https://example.com/dart.png";
            defaultTheme.BalloonAnvilImageUrl = "https://example.com/anvil.png";
            defaultTheme.BalloonNudgeImageUrl = "https://example.com/nudge.png";
            defaultTheme.BalloonWindGustImageUrl = "https://example.com/wind.png";

            await settingsService.SaveSettingsAsync(settings);

            _emoteService.Setup(e => e.GetEmotesAsync())
                .ReturnsAsync(new Dictionary<string, string>
                {
                    ["dart_emote"] = "https://cdn.example.com/dart_emote.png",
                    ["anvil_emote"] = "https://cdn.example.com/anvil_emote.png"
                });

            var service = CreateBalloonService(dropRegistry, settingsService);

            var dartHazards = await service.SpawnHazardsAsync(new BalloonHazardSpawnRequest
            {
                Count = 2,
                HazardType = "DartPop"
            });

            Assert.Equal(2, dartHazards.Count);
            Assert.All(dartHazards, h =>
            {
                Assert.Equal("DartPop", h.HazardType);
                Assert.Equal("https://cdn.example.com/dart_emote.png", h.ImageUrl);
            });

            var anvilHazards = await service.SpawnHazardsAsync(new BalloonHazardSpawnRequest
            {
                Count = 2,
                HazardType = "HeavyPush"
            });

            Assert.Equal(2, anvilHazards.Count);
            Assert.All(anvilHazards, h =>
            {
                Assert.Equal("HeavyPush", h.HazardType);
                Assert.Equal("https://cdn.example.com/anvil_emote.png", h.ImageUrl);
            });
        }

        [Fact]
        public void BalloonAscentSettings_And_Theme_SupportCloudSpeedAndHazardProperties()
        {
            var settings = new BalloonAscentGameSettings
            {
                TargetSpeed = 1.8,
                DefaultDartEmote = "dart1",
                DefaultAnvilEmote = "anvil1",
                DefaultNudgeEmote = "nudge1",
                DefaultWindGustEmote = "wind1"
            };

            Assert.Equal(1.8, settings.TargetSpeed);
            Assert.Equal("dart1", settings.DefaultDartEmote);
            Assert.Equal("anvil1", settings.DefaultAnvilEmote);
            Assert.Equal("nudge1", settings.DefaultNudgeEmote);
            Assert.Equal("wind1", settings.DefaultWindGustEmote);

            var theme = new DropGameTheme
            {
                BalloonDartImageUrl = "/images/dart.png",
                BalloonAnvilImageUrl = "/images/anvil.png",
                BalloonNudgeImageUrl = "/images/nudge.png",
                BalloonWindGustImageUrl = "/images/wind.png",
                BalloonCloudPlatformImageUrl = "/images/cloud.png",
                BalloonCanopyImageUrl = "/images/canopy.png"
            };

            Assert.Equal("/images/dart.png", theme.BalloonDartImageUrl);
            Assert.Equal("/images/anvil.png", theme.BalloonAnvilImageUrl);
            Assert.Equal("/images/nudge.png", theme.BalloonNudgeImageUrl);
            Assert.Equal("/images/wind.png", theme.BalloonWindGustImageUrl);
            Assert.Equal("/images/cloud.png", theme.BalloonCloudPlatformImageUrl);
            Assert.Equal("/images/canopy.png", theme.BalloonCanopyImageUrl);
        }

        [Fact]
        public async Task BalloonAscentService_StopSessionAsync_SequentiallyStartsQueuedSession()
        {
            var dropRegistry = CreateDropRegistry();
            var settingsService = CreateSettingsService();
            var service = CreateBalloonService(dropRegistry, settingsService);

            // 1. Start active Balloon session 1 (50s)
            var s1 = await service.StartSessionAsync(50, triggeredBy: "Streamer");
            Assert.True(s1.IsActive);
            Assert.False(s1.IsQueued);
            Assert.Equal(0, service.GetQueuedSessionCount());

            // 2. Start Balloon session 2 (30s) while session 1 is active -> should be queued
            var s2 = await service.StartSessionAsync(30, triggeredBy: "Mod");
            Assert.False(s2.IsActive);
            Assert.True(s2.IsQueued);
            Assert.Equal(1, s2.QueuePosition);
            Assert.Equal(1, service.GetQueuedSessionCount());

            // 3. Stop session 1 -> should promote queued session 2 sequentially without deadlocking
            var stopped1 = await service.StopSessionAsync(triggeredBy: "Streamer");
            Assert.NotNull(stopped1);
            Assert.Equal(50, stopped1.TotalDurationSeconds);

            var promoted = service.GetCurrentSession();
            Assert.NotNull(promoted);
            Assert.True(promoted.IsActive);
            Assert.Equal(30, promoted.TotalDurationSeconds);
            Assert.Equal(0, service.GetQueuedSessionCount());

            // 4. Stop promoted session 2
            var stopped2 = await service.StopSessionAsync(triggeredBy: "Streamer");
            Assert.NotNull(stopped2);
            Assert.Null(service.GetCurrentSession());
        }

        [Fact]
        public async Task BalloonAscentService_SpawnHazardsAsync_EnforcesSettingsAndTargetUser()
        {
            var dropRegistry = CreateDropRegistry();
            var settingsService = CreateSettingsService();
            var settings = await settingsService.GetSettingsAsync();

            settings.Balloon.EnableHazards = false;
            await settingsService.SaveSettingsAsync(settings);

            var service = CreateBalloonService(dropRegistry, settingsService);

            // 1. When EnableHazards is false, returns empty list
            var disabledResult = await service.SpawnHazardsAsync(new BalloonHazardSpawnRequest
            {
                Count = 5,
                HazardType = "Random"
            });
            Assert.Empty(disabledResult);

            // Re-enable hazards, but disable darts
            settings.Balloon.EnableHazards = true;
            settings.Balloon.EnableDarts = false;
            await settingsService.SaveSettingsAsync(settings);

            // 2. Explicit request for disabled hazard type returns empty list
            var dartResult = await service.SpawnHazardsAsync(new BalloonHazardSpawnRequest
            {
                Count = 3,
                HazardType = "DartPop"
            });
            Assert.Empty(dartResult);

            // Register active drop for targeting
            var now = DateTime.UtcNow;
            var targetRecord = new PendingDropRecord
            {
                DropToken = "target-token-123",
                GameType = DropGameType.Balloon,
                Username = "target_pilot",
                DisplayName = "TargetPilot",
                UserId = "target-user-id",
                EventArgs = new CommandEventArgs { Command = "balloon", Name = "target_pilot", DisplayName = "TargetPilot", UserId = "target-user-id" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now,
                EarliestCompletionUtc = now.AddSeconds(-2),
                ExpiresAtUtc = now.AddSeconds(60),
                StartX = 0.40,
                StartY = 0.94,
                LandingY = 0.10,
                TargetCenterX = 0.50,
                TotalTicks = 180
            };
            dropRegistry.RegisterDrop(targetRecord);

            // 3. TargetUser selects matching drop case-insensitively
            var targetedResult = await service.SpawnHazardsAsync(new BalloonHazardSpawnRequest
            {
                Count = 1,
                HazardType = "Nudge",
                Pattern = "Targeted",
                TargetUser = "TARGET_PILOT"
            });
            Assert.NotEmpty(targetedResult);
        }

        [Theory]
        [InlineData(150, 100)]
        [InlineData(-25, 0)]
        [InlineData(85, 85)]
        public async Task DropGameCompleteHandler_Balloon_ClampsScoreToInclusive0To100(int reportedScore, int expectedScore)
        {
            var dropRegistry = CreateDropRegistry();
            var settingsService = CreateSettingsService();
            var balloonServiceMock = new Mock<IBalloonAscentService>();
            var coordinatorMock = new Mock<IFeatureRuntimeCoordinator>();
            coordinatorMock.Setup(c => c.IsEnabled(FeatureKeys.DropGames)).Returns(true);
            var securityService = CreateOverlaySecurityService();
            var validKey = await securityService.GetOverlayKeyAsync();

            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = "clamp-token-" + reportedScore,
                GameType = DropGameType.Balloon,
                Username = "clamp_user",
                DisplayName = "ClampUser",
                UserId = "clamp-user-id",
                EventArgs = new CommandEventArgs { Command = "balloon", Name = "clamp_user", DisplayName = "ClampUser", UserId = "clamp-user-id" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now.AddSeconds(-6),
                EarliestCompletionUtc = now.AddSeconds(-1),
                ExpiresAtUtc = now.AddSeconds(60)
            };
            dropRegistry.RegisterDrop(record);

            var handler = new DropGameCompleteHandler(
                dropRegistry,
                Mock.Of<IParachuteDropService>(),
                Mock.Of<IPlinkoDropService>(),
                settingsService,
                securityService,
                coordinatorMock.Object,
                balloonServiceMock.Object);

            var notification = new DropGameCompleteNotification(
                record.DropToken,
                score: reportedScore,
                isWin: true,
                overlayKey: validKey);

            await handler.Handle(notification, CancellationToken.None);

            Assert.Equal(expectedScore, record.Score);
        }

        [Fact]
        public async Task DropGameSessionManager_LifecycleAndQueueing_WorksAsExpected()
        {
            var manager = new DropGameSessionManager(NullLogger.Instance);

            Assert.Null(manager.ActiveSession);
            Assert.False(manager.HasActiveTimedSession());
            Assert.Equal(0, manager.QueuedCount);

            var tcs = new TaskCompletionSource<bool>();
            var session = manager.CreateAndSetActiveSession(60, "theme1", "UserA", state =>
            {
                tcs.TrySetResult(true);
                return Task.CompletedTask;
            });

            Assert.NotNull(session);
            Assert.True(manager.HasActiveTimedSession());
            Assert.Equal("theme1", session.ThemeIdOverride);
            Assert.Equal("UserA", session.StartedBy);
            Assert.Equal(60, session.TotalDurationSeconds);

            var dto = manager.GetCurrentSessionDto("test");
            Assert.NotNull(dto);
            Assert.True(dto.IsActive);
            Assert.Equal("test", dto.GameType);

            // Queueing
            var req1 = manager.EnqueueSession(30, "theme2", "UserB");
            var req2 = manager.EnqueueSession(45, "theme3", "UserC");
            Assert.Equal(2, manager.QueuedCount);

            Assert.True(manager.TryDequeueNextQueuedSession(out var next));
            Assert.NotNull(next);
            Assert.Equal("UserB", next.TriggeredBy);
            Assert.Equal(1, manager.QueuedCount);

            // User drop limits & drop recording
            Assert.True(manager.CanUserDrop("User1", 2));
            manager.RecordUserDrop("User1");
            Assert.True(manager.CanUserDrop("User1", 2));
            manager.RecordUserDrop("User1");
            Assert.False(manager.CanUserDrop("User1", 2));
            Assert.True(manager.CanUserDrop("User2", 2));

            // Stop session
            var stopped = manager.ExchangeStopSession();
            Assert.NotNull(stopped);
            Assert.Null(manager.ActiveSession);
            Assert.False(manager.HasActiveTimedSession());

            // Clear remaining queue
            var removed = manager.RemoveQueuedSessions();
            Assert.Equal(1, removed);
            Assert.Equal(0, manager.QueuedCount);

            manager.Dispose();
        }

        [Fact]
        public void DropGameSessionManager_IsWithinLastCallCutoff_EvaluatesRemainingDurationCorrectly()
        {
            var manager = new DropGameSessionManager(NullLogger.Instance);

            // No active session
            Assert.False(manager.IsWithinLastCallCutoff(4, out _));

            // Active session with 60 seconds remaining
            var session = manager.CreateAndSetActiveSession(60, null, "UserA", _ => Task.CompletedTask);
            Assert.False(manager.IsWithinLastCallCutoff(4, out var remaining1));
            Assert.True(remaining1 > 50.0);

            // Cutoff is 0 (disabled)
            Assert.False(manager.IsWithinLastCallCutoff(0, out _));

            // Set session near expiration (2 seconds remaining)
            session.EndsAtUtc = DateTime.UtcNow.AddSeconds(2);
            Assert.True(manager.IsWithinLastCallCutoff(4, out var remaining2));
            Assert.True(remaining2 <= 4.0);

            manager.Dispose();
        }

        [Fact]
        public async Task DropGameBroadcastCoordinator_BroadcastMethods_RoutePayloadsToAllOtherTopics()
        {
            var mockWs = new Mock<IWebSocketMessenger>();
            var queuedMessages = new List<(string Topic, string Json)>();
            mockWs.Setup(x => x.AddToQueue(It.IsAny<string>(), It.IsAny<string>()))
                  .Callback<string, string>((topic, json) => queuedMessages.Add((topic, json)))
                  .Returns(Task.CompletedTask);

            // Broadcast game status for curling -> should notify parachute, plinko, balloon
            await DropGameBroadcastCoordinator.BroadcastOtherGameStatusAsync(
                mockWs.Object, NullLogger.Instance, WsTopics.Curling, "curling", isOtherGameActive: true);

            var topicsNotified = queuedMessages.Select(m => m.Topic).Distinct().ToList();
            Assert.Contains(WsTopics.Parachute, topicsNotified);
            Assert.Contains(WsTopics.Plinko, topicsNotified);
            Assert.Contains(WsTopics.Balloon, topicsNotified);
            Assert.DoesNotContain(WsTopics.Curling, topicsNotified);

            var firstJson = queuedMessages[0].Json;
            Assert.Contains("\"type\":\"other_game_status\"", firstJson);
            Assert.Contains("\"isOtherGameActive\":true", firstJson);
            Assert.Contains("\"otherGame\":\"curling\"", firstJson);

            queuedMessages.Clear();

            // Broadcast drop active for balloon -> should notify parachute, plinko, curling
            await DropGameBroadcastCoordinator.BroadcastOtherDropActiveAsync(
                mockWs.Object, NullLogger.Instance, WsTopics.Balloon, durationSeconds: 8.5);

            var dropTopics = queuedMessages.Select(m => m.Topic).Distinct().ToList();
            Assert.Contains(WsTopics.Parachute, dropTopics);
            Assert.Contains(WsTopics.Plinko, dropTopics);
            Assert.Contains(WsTopics.Curling, dropTopics);
            Assert.DoesNotContain(WsTopics.Balloon, dropTopics);

            var dropJson = queuedMessages[0].Json;
            Assert.Contains("\"type\":\"other_drop_active\"", dropJson);
            Assert.Contains("\"durationSeconds\":8.5", dropJson);
        }

        [Fact]
        public void DropGameHeaderConfigDto_FromSettings_MapsCorrectly()
        {
            var pSettings = new ParachuteGameSettings
            {
                ShowHeaderBanner = true,
                ShowTimer = true,
                ShowDropCount = false,
                ShowFreePlayCounter = true,
                AutoAvoidOverlap = true,
                HeaderPosition = "Bottom",
                HeaderCustomYOffset = 42,
                NameFontSize = 18
            };
            var pHeader = DropGameHeaderConfigDto.FromSettings(pSettings);
            Assert.True(pHeader.ShowBanner);
            Assert.True(pHeader.ShowTimer);
            Assert.False(pHeader.ShowCount);
            Assert.True(pHeader.ShowFreePlayCounter);
            Assert.True(pHeader.AutoAvoidOverlap);
            Assert.Equal("Bottom", pHeader.Position);
            Assert.Equal(42, pHeader.YOffset);
            Assert.Equal(18, pHeader.NameFontSize);

            var plinkoSettings = new PlinkoGameSettings
            {
                ShowHeaderBanner = false,
                ShowTimer = false,
                ShowTokenCount = true,
                ShowFreePlayCounter = false,
                AutoAvoidOverlap = false,
                HeaderPosition = "Top",
                HeaderCustomYOffset = 10,
                NameFontSize = 22
            };
            var plinkoHeader = DropGameHeaderConfigDto.FromSettings(plinkoSettings);
            Assert.False(plinkoHeader.ShowBanner);
            Assert.False(plinkoHeader.ShowTimer);
            Assert.True(plinkoHeader.ShowCount);
            Assert.False(plinkoHeader.ShowFreePlayCounter);
            Assert.False(plinkoHeader.AutoAvoidOverlap);
            Assert.Equal("Top", plinkoHeader.Position);
            Assert.Equal(10, plinkoHeader.YOffset);
            Assert.Equal(22, plinkoHeader.NameFontSize);

            var bSettings = new BalloonAscentGameSettings
            {
                ShowHeaderBanner = true,
                ShowTimer = true,
                ShowDropCount = true,
                ShowFreePlayCounter = true,
                AutoAvoidOverlap = true,
                HeaderPosition = "Top",
                HeaderCustomYOffset = 15,
                NameFontSize = 19
            };
            var bHeader = DropGameHeaderConfigDto.FromSettings(bSettings);
            Assert.True(bHeader.ShowBanner);
            Assert.True(bHeader.ShowTimer);
            Assert.True(bHeader.ShowCount);
            Assert.True(bHeader.ShowFreePlayCounter);
            Assert.True(bHeader.AutoAvoidOverlap);
            Assert.Equal("Top", bHeader.Position);
            Assert.Equal(15, bHeader.YOffset);
            Assert.Equal(19, bHeader.NameFontSize);

            var cSettings = new CurlingGameSettings
            {
                ShowHeaderBanner = true,
                ShowTimer = true,
                ShowDropCount = true,
                ShowFreePlayCounter = false,
                AutoAvoidOverlap = true,
                HeaderPosition = "TopCenter",
                HeaderCustomYOffset = 20,
                NameFontSize = 18
            };
            var cHeader = DropGameHeaderConfigDto.FromSettings(cSettings);
            Assert.True(cHeader.ShowBanner);
            Assert.True(cHeader.ShowTimer);
            Assert.True(cHeader.ShowCount);
            Assert.False(cHeader.ShowFreePlayCounter);
            Assert.True(cHeader.AutoAvoidOverlap);
            Assert.Equal("TopCenter", cHeader.Position);
            Assert.Equal(20, cHeader.YOffset);
            Assert.Equal(18, cHeader.NameFontSize);
        }

        [Fact]
        public async Task DropGameCompleteHandler_Curling_Authorized_TrustsClientOutcome()
        {
            var dropRegistry = CreateDropRegistry();
            var settingsService = CreateSettingsService();
            var parachuteServiceMock = new Mock<IParachuteDropService>();
            var plinkoServiceMock = new Mock<IPlinkoDropService>();
            var balloonServiceMock = new Mock<IBalloonAscentService>();
            var curlingServiceMock = new Mock<ICurlingGameService>();
            var coordinatorMock = new Mock<IFeatureRuntimeCoordinator>();
            coordinatorMock.Setup(c => c.IsEnabled(FeatureKeys.DropGames)).Returns(true);
            var securityService = CreateOverlaySecurityService();
            var validKey = await securityService.GetOverlayKeyAsync();

            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = "curling-auth-token",
                GameType = DropGameType.Curling,
                Username = "curler_1",
                DisplayName = "Curler1",
                UserId = "user-curl-1",
                EventArgs = new CommandEventArgs { Command = "curl", Name = "curler_1", DisplayName = "Curler1", UserId = "user-curl-1" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now.AddSeconds(-6),
                EarliestCompletionUtc = now.AddSeconds(-1),
                ExpiresAtUtc = now.AddSeconds(60)
            };

            dropRegistry.RegisterDrop(record);

            var handler = new DropGameCompleteHandler(
                dropRegistry,
                parachuteServiceMock.Object,
                plinkoServiceMock.Object,
                settingsService,
                securityService,
                coordinatorMock.Object,
                balloonServiceMock.Object,
                curlingServiceMock.Object);

            var notification = new DropGameCompleteNotification(
                "curling-auth-token",
                landingX: 0.842,
                score: 100,
                isBullseye: true,
                isWin: true,
                isHit: true,
                overlayKey: validKey);

            await handler.Handle(notification, CancellationToken.None);

            curlingServiceMock.Verify(s => s.ProcessCompletionAsync(It.Is<PendingDropRecord>(r =>
                r.DropToken == "curling-auth-token" &&
                r.Score == 100 &&
                r.IsBullseye == true &&
                r.IsWin == true &&
                r.IsHit == true &&
                r.LandingX == 0.842)), Times.Once);
        }

        [Fact]
        public async Task DropGameCompleteHandler_Curling_Unauthorized_ReturnsEarly()
        {
            var dropRegistry = CreateDropRegistry();
            var settingsService = CreateSettingsService();
            var parachuteServiceMock = new Mock<IParachuteDropService>();
            var plinkoServiceMock = new Mock<IPlinkoDropService>();
            var balloonServiceMock = new Mock<IBalloonAscentService>();
            var curlingServiceMock = new Mock<ICurlingGameService>();
            var coordinatorMock = new Mock<IFeatureRuntimeCoordinator>();
            coordinatorMock.Setup(c => c.IsEnabled(FeatureKeys.DropGames)).Returns(true);
            var securityService = CreateOverlaySecurityService();

            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = "curling-unauth-token",
                GameType = DropGameType.Curling,
                Username = "attacker",
                DisplayName = "Attacker",
                UserId = "attacker-1",
                EventArgs = new CommandEventArgs { Command = "curl", Name = "attacker", DisplayName = "Attacker", UserId = "attacker-1" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now.AddSeconds(-6),
                EarliestCompletionUtc = now.AddSeconds(-1),
                ExpiresAtUtc = now.AddSeconds(60)
            };

            dropRegistry.RegisterDrop(record);

            var handler = new DropGameCompleteHandler(
                dropRegistry,
                parachuteServiceMock.Object,
                plinkoServiceMock.Object,
                settingsService,
                securityService,
                coordinatorMock.Object,
                balloonServiceMock.Object,
                curlingServiceMock.Object);

            var notification = new DropGameCompleteNotification(
                "curling-unauth-token",
                landingX: 0.84,
                score: 100,
                isBullseye: true,
                isWin: true,
                overlayKey: "invalid-key");

            await handler.Handle(notification, CancellationToken.None);

            curlingServiceMock.Verify(s => s.ProcessCompletionAsync(It.IsAny<PendingDropRecord>()), Times.Never);
        }

        [Fact]
        public async Task CurlingGameService_TriggerCurlingSlideAsync_RegistersDropAndBroadcasts()
        {
            var dropRegistry = CreateDropRegistry();
            var service = CreateCurlingService(dropRegistry);

            var args = new CommandEventArgs
            {
                Command = "curl",
                Name = "test_curler",
                DisplayName = "TestCurler",
                UserId = "curler-100",
                Args = ["85"]
            };

            var token = await service.TriggerCurlingSlideAsync(args, power: 85.0);

            Assert.False(string.IsNullOrWhiteSpace(token));
            var registered = dropRegistry.GetPendingDrop(token);
            Assert.NotNull(registered);
            Assert.Equal(DropGameType.Curling, registered.GameType);
            Assert.Equal("test_curler", registered.Username);
        }

        [Fact]
        public async Task CurlingGameService_CenterTarget_StoresEffectiveTargetDistanceOnPendingRecord()
        {
            var dropRegistry = CreateDropRegistry();
            var settingsService = CreateSettingsService();
            var settings = await settingsService.GetSettingsAsync();
            settings.Curling.SlideDirection = CurlingSlideDirection.CenterTarget;
            settings.Curling.TargetDistancePercent = 84.0;
            await settingsService.SaveSettingsAsync(settings);
            var service = CreateCurlingService(dropRegistry, settingsService);
            var args = new CommandEventArgs
            {
                Command = "curl",
                Name = "center_curler",
                DisplayName = "CenterCurler",
                UserId = "curler-center"
            };

            var token = await service.TriggerCurlingSlideAsync(args, power: 80.0);

            var registered = dropRegistry.GetPendingDrop(token);
            Assert.NotNull(registered);
            Assert.Equal(50.0, registered.TargetDistancePercent);
        }

        [Fact]
        public async Task CurlingGameService_StartAndStopSession_ManagesActiveState()
        {
            var dropRegistry = CreateDropRegistry();
            var service = CreateCurlingService(dropRegistry);

            var session = await service.StartSessionAsync(60, triggeredBy: "TestMod");

            Assert.NotNull(session);
            Assert.True(session.IsActive);
            Assert.Equal("curling", session.GameType);
            Assert.True(dropRegistry.IsSessionActive(DropGameType.Curling));

            var current = service.GetCurrentSession();
            Assert.NotNull(current);
            Assert.True(current.IsActive);

            var stopped = await service.StopSessionAsync("TestMod");

            Assert.NotNull(stopped);
            Assert.False(stopped.IsActive);
            Assert.False(dropRegistry.IsSessionActive(DropGameType.Curling));
            Assert.Null(service.GetCurrentSession());
        }

        [Fact]
        public async Task CurlingGameService_LastCallCutoff_RejectsLateThrows()
        {
            var dropRegistry = CreateDropRegistry();
            var settingsService = CreateSettingsService();
            var settings = await settingsService.GetSettingsAsync();
            settings.Curling.LastCallCutoffSeconds = 10;
            settings.Curling.DefaultSessionDurationSeconds = 15;

            var service = CreateCurlingService(dropRegistry, settingsService);

            // Start 10-second session (minimum session duration)
            await service.StartSessionAsync(10, triggeredBy: "TestMod");

            var args = new CommandEventArgs
            {
                Command = "curl",
                Name = "buzzer_beater",
                DisplayName = "BuzzerBeater",
                UserId = "bb-1"
            };

            // Session has 10s total duration <= 10s cutoff, so throw should be rejected
            var token = await service.TriggerCurlingSlideAsync(args);

            // Should not register in registry because it was rejected by Last Call cutoff
            var registered = dropRegistry.GetPendingDrop(token);
            Assert.Null(registered);

            await service.StopSessionAsync("Cleanup");
        }

        [Fact]
        public async Task MultiGameActiveList_TrackedAndBroadcastCorrectly()
        {
            var dropRegistry = CreateDropRegistry();
            var settingsService = CreateSettingsService();

            var parachuteService = CreateParachuteService(settingsService, dropRegistry);
            var plinkoService = CreatePlinkoService(settingsService, dropRegistry);
            var curlingService = CreateCurlingService(dropRegistry, settingsService);

            // 1. Start Parachute alone
            var parachuteSession = await parachuteService.StartSessionAsync(60, triggeredBy: "Mod");
            Assert.True(parachuteSession.IsActive);
            Assert.False(parachuteSession.IsOtherGameActive);
            Assert.Contains("parachute", parachuteSession.ActiveGames);
            Assert.Single(parachuteSession.ActiveGames);

            // 2. Start Plinko concurrently
            var plinkoSession = await plinkoService.StartSessionAsync(60, triggeredBy: "Mod");
            Assert.True(plinkoSession.IsActive);
            Assert.True(plinkoSession.IsOtherGameActive);
            Assert.Contains("parachute", plinkoSession.ActiveGames);
            Assert.Contains("plinko", plinkoSession.ActiveGames);
            Assert.Equal(2, plinkoSession.ActiveGames.Count);

            // 3. Start Curling concurrently (3 games running simultaneously)
            var curlingSession = await curlingService.StartSessionAsync(60, triggeredBy: "Mod");
            Assert.True(curlingSession.IsActive);
            Assert.True(curlingSession.IsOtherGameActive);
            Assert.Contains("parachute", curlingSession.ActiveGames);
            Assert.Contains("plinko", curlingSession.ActiveGames);
            Assert.Contains("curling", curlingSession.ActiveGames);
            Assert.Equal(3, curlingSession.ActiveGames.Count);

            // Verify registry tracks all 3 session types
            var activeTypes = dropRegistry.GetActiveSessionTypes();
            Assert.Equal(3, activeTypes.Count);
            Assert.Contains(DropGameType.Parachute, activeTypes);
            Assert.Contains(DropGameType.Plinko, activeTypes);
            Assert.Contains(DropGameType.Curling, activeTypes);

            // 4. Stop Parachute, verify remaining active games
            var stoppedParachute = await parachuteService.StopSessionAsync("Mod");
            Assert.NotNull(stoppedParachute);
            Assert.False(stoppedParachute.IsActive);
            Assert.True(stoppedParachute.IsOtherGameActive);
            Assert.DoesNotContain("parachute", stoppedParachute.ActiveGames);
            Assert.Contains("plinko", stoppedParachute.ActiveGames);
            Assert.Contains("curling", stoppedParachute.ActiveGames);

            var remainingTypes = dropRegistry.GetActiveSessionTypes();
            Assert.Equal(2, remainingTypes.Count);
            Assert.DoesNotContain(DropGameType.Parachute, remainingTypes);

            // Clean up
            await plinkoService.StopSessionAsync("Cleanup");
            await curlingService.StopSessionAsync("Cleanup");
        }

        [Fact]
        public async Task ParsePowerAndEmoteAsync_HandlesEmoteWithoutPower_AndPowerWithTypos()
        {
            _emoteService.Setup(x => x.GetEmotesAsync()).ReturnsAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sptv318"] = "https://static-cdn.jtvnw.net/emoticons/v2/318/default/dark/3.0"
            });

            // Case 1: Viewer omits power and passes an alphanumeric emote (!golf sptv318)
            var emoteOnlyArgs = new CommandEventArgs
            {
                Command = "golf",
                Name = "viewer1",
                DisplayName = "Viewer1",
                Arg = "sptv318",
                Args = ["sptv318"],
                EmoteUrl = "https://static-cdn.jtvnw.net/emoticons/v2/318/default/dark/3.0",
                Fragments =
                [
                    new ChatOverlayFragment { Type = "text", Text = "!golf " },
                    new ChatOverlayFragment { Type = "emote", Text = "sptv318", EmoteId = "318" }
                ]
            };

            var (power1, emote1) = await DropGameMediaResolver.ParsePowerAndEmoteAsync(
                _emoteService.Object,
                emoteOnlyArgs,
                explicitPower: null,
                explicitEmote: null,
                enableViewerPowerControl: true,
                defaultPowerMin: 65,
                defaultPowerMax: 85);
            Assert.InRange(power1, 65.0, 85.0);
            Assert.Equal("sptv318", emote1);

            // Also verify ResolveCharacterOrEmoteUrlAsync resolves the native Twitch emote when emoteOrImageInput ("sptv318") is passed!
            var resolvedUrl = await DropGameMediaResolver.ResolveCharacterOrEmoteUrlAsync(_emoteService.Object, emoteOnlyArgs, emote1, "/fallback.png");
            Assert.Equal("https://static-cdn.jtvnw.net/emoticons/v2/318/default/dark/1.0", resolvedUrl);

            // Case 2: Viewer provides power with a minor typo (75%) and an emote (!golf 75% sptv318)
            var typoPowerArgs = new CommandEventArgs
            {
                Command = "golf",
                Name = "viewer1",
                DisplayName = "Viewer1",
                Arg = "75% sptv318",
                Args = ["75%", "sptv318"]
            };

            var (power2, emote2) = await DropGameMediaResolver.ParsePowerAndEmoteAsync(
                _emoteService.Object,
                typoPowerArgs,
                explicitPower: null,
                explicitEmote: null,
                enableViewerPowerControl: true,
                defaultPowerMin: 65,
                defaultPowerMax: 85);
            Assert.Equal(75.0, power2);
            Assert.Equal("sptv318", emote2);

            // Case 3: Viewer reverses order (!golf sptv318 80)
            var reversedArgs = new CommandEventArgs
            {
                Command = "golf",
                Name = "viewer1",
                DisplayName = "Viewer1",
                Arg = "sptv318 80",
                Args = ["sptv318", "80"]
            };

            var (power3, emote3) = await DropGameMediaResolver.ParsePowerAndEmoteAsync(
                _emoteService.Object,
                reversedArgs,
                explicitPower: null,
                explicitEmote: null,
                enableViewerPowerControl: true,
                defaultPowerMin: 65,
                defaultPowerMax: 85);
            Assert.Equal(80.0, power3);
            Assert.Equal("sptv318", emote3);
        }

        private static CommandEventArgs CreateGolfArgs() => new()
        {
            Command = "golf",
            Name = "golfer_multi",
            DisplayName = "GolferMulti",
            UserId = "golfer-1"
        };

        private static PendingDropRecord CreateGolfOutcomeRecord(CommandEventArgs args, int strokeNumber, bool isWin, bool isBullseye = false, string? dropToken = null)
        {
            var now = DateTime.UtcNow;
            return new PendingDropRecord
            {
                DropToken = dropToken ?? Guid.NewGuid().ToString("N"),
                GameType = DropGameType.Golf,
                Username = args.Name,
                DisplayName = args.DisplayName,
                UserId = args.UserId,
                StrokeNumber = strokeNumber,
                IsWin = isWin || isBullseye,
                IsBullseye = isBullseye,
                Score = isWin || isBullseye ? 50 : 0,
                EventArgs = args,
                Theme = new DropGameTheme(),
                CreatedAtUtc = now,
                EarliestCompletionUtc = now,
                ExpiresAtUtc = now.AddMinutes(1)
            };
        }

        private async Task<GolfGameService> CreateMultiStrokeGolfServiceAsync(int maxStrokes, CommandEventArgs args, Action<string>? onFirstShotStarted = null)
        {
            var settingsService = CreateSettingsService();
            var settings = await settingsService.GetSettingsAsync();
            settings.Golf.MaxStrokesPerPlayer = maxStrokes;
            settings.Golf.PowerVariancePercent = 0.0;
            await settingsService.SaveSettingsAsync(settings);

            var golfService = CreateGolfService(CreateDropRegistry(), settingsService);

            // First shot registers the player's round state (multi-stroke tracking); it is intentionally never completed
            var firstDropToken = await golfService.TriggerGolfShotAsync(args, power: 40.0, conditionOverride: GolfGreenCondition.StandardGreen, targetDistanceOverride: 84.0);
            onFirstShotStarted?.Invoke(firstDropToken);
            return golfService;
        }

        private void VerifyGolfEvent(string eventType, Times times)
        {
            _defaultCommandTriggerService.Verify(x => x.TriggerDefaultCommandEventAsync(
                "golf",
                eventType,
                It.IsAny<CommandEventArgs>(),
                It.IsAny<Dictionary<string, string>?>()), times);
        }

        [Fact]
        public async Task GolfGameService_MultiStroke_NonFinalStroke_DefersWinOutcome()
        {
            var args = CreateGolfArgs();
            var golfService = await CreateMultiStrokeGolfServiceAsync(3, args);

            await golfService.ProcessCompletionAsync(CreateGolfOutcomeRecord(args, strokeNumber: 1, isWin: true));

            VerifyGolfEvent(DefaultCommandEventTypes.GolfWin, Times.Never());
            VerifyGolfEvent(DefaultCommandEventTypes.GolfLose, Times.Never());
            VerifyGolfEvent(DefaultCommandEventTypes.GolfBullseye, Times.Never());
        }

        [Fact]
        public async Task GolfGameService_MultiStroke_NonFinalLoseStroke_AlsoDefersLoseOutcome()
        {
            var args = CreateGolfArgs();
            var golfService = await CreateMultiStrokeGolfServiceAsync(3, args);

            await golfService.ProcessCompletionAsync(CreateGolfOutcomeRecord(args, strokeNumber: 1, isWin: false));

            VerifyGolfEvent(DefaultCommandEventTypes.GolfLose, Times.Never());
        }

        [Fact]
        public async Task GolfGameService_MultiStroke_FinalStroke_FiresOnceAndDropsEarlierDeferredOutcome()
        {
            var args = CreateGolfArgs();
            var golfService = await CreateMultiStrokeGolfServiceAsync(2, args);
            golfService.FreePlayRoundIdleTimeout = TimeSpan.FromMilliseconds(150);

            await golfService.ProcessCompletionAsync(CreateGolfOutcomeRecord(args, strokeNumber: 1, isWin: true));
            await golfService.ProcessCompletionAsync(CreateGolfOutcomeRecord(args, strokeNumber: 2, isWin: false));

            // Wait past the idle timeout: the stale stroke-1 win must NOT fire after the round already finished
            await Task.Delay(500);

            VerifyGolfEvent(DefaultCommandEventTypes.GolfLose, Times.Once());
            VerifyGolfEvent(DefaultCommandEventTypes.GolfWin, Times.Never());
        }

        [Fact]
        public async Task GolfGameService_MultiStroke_BullseyeWithStrokesLeft_FiresImmediately()
        {
            var args = CreateGolfArgs();
            var golfService = await CreateMultiStrokeGolfServiceAsync(3, args);

            await golfService.ProcessCompletionAsync(CreateGolfOutcomeRecord(args, strokeNumber: 1, isWin: true, isBullseye: true));

            VerifyGolfEvent(DefaultCommandEventTypes.GolfBullseye, Times.Once());
            VerifyGolfEvent(DefaultCommandEventTypes.GolfWin, Times.Once());
            VerifyGolfEvent(DefaultCommandEventTypes.GolfLose, Times.Never());
        }

        [Fact]
        public async Task GolfGameService_SingleStrokeMode_FiresOutcomeImmediately()
        {
            var args = CreateGolfArgs();
            var golfService = await CreateMultiStrokeGolfServiceAsync(1, args);

            await golfService.ProcessCompletionAsync(CreateGolfOutcomeRecord(args, strokeNumber: 1, isWin: true));

            VerifyGolfEvent(DefaultCommandEventTypes.GolfWin, Times.Once());
        }

        [Fact]
        public async Task GolfGameService_OutcomeIncludesStrokeAndShotContextAliases()
        {
            var args = CreateGolfArgs();
            var golfService = await CreateMultiStrokeGolfServiceAsync(3, args);
            var record = CreateGolfOutcomeRecord(args, strokeNumber: 1, isWin: true, isBullseye: true);
            record.ConditionLabel = "Fast Links";
            record.TargetDistancePercent = 84.0;

            await golfService.ProcessCompletionAsync(record);

            _defaultCommandTriggerService.Verify(x => x.TriggerDefaultCommandEventAsync(
                "golf",
                DefaultCommandEventTypes.GolfWin,
                It.IsAny<CommandEventArgs>(),
                It.Is<Dictionary<string, string>?>(variables =>
                    variables != null &&
                    variables["StrokeNumber"] == "1" &&
                    variables["Strokes"] == "1" &&
                    variables["GreenCondition"] == "Fast Links" &&
                    variables["PinDistance"] == "84.0")), Times.Once());
        }

        [Fact]
        public async Task GolfGameService_MultiStroke_InFlightShotCompletingAfterSessionStop_IsDiscarded()
        {
            var args = CreateGolfArgs();
            var golfService = await CreateMultiStrokeGolfServiceAsync(3, args);

            await golfService.StartSessionAsync(60, triggeredBy: "test");
            await golfService.TriggerGolfShotAsync(args, power: 40.0, conditionOverride: GolfGreenCondition.StandardGreen, targetDistanceOverride: 84.0);

            await golfService.StopSessionAsync("test");

            // Shot was still in flight when the session stopped; its late completion must not fire anything
            await golfService.ProcessCompletionAsync(CreateGolfOutcomeRecord(args, strokeNumber: 1, isWin: true));
            await golfService.ProcessCompletionAsync(CreateGolfOutcomeRecord(args, strokeNumber: 1, isWin: false));

            VerifyGolfEvent(DefaultCommandEventTypes.GolfWin, Times.Never());
            VerifyGolfEvent(DefaultCommandEventTypes.GolfLose, Times.Never());
        }

        [Fact]
        public async Task GolfGameService_MultiStroke_FreePlay_IdleFinalizedRound_DiscardsDuplicateAndStartsNewHole()
        {
            var args = CreateGolfArgs();
            var firstDropToken = "";
            var golfService = await CreateMultiStrokeGolfServiceAsync(3, args, token => firstDropToken = token);
            golfService.FreePlayRoundIdleTimeout = TimeSpan.FromMilliseconds(100);

            var record = CreateGolfOutcomeRecord(args, strokeNumber: 1, isWin: true, dropToken: firstDropToken);
            await golfService.ProcessCompletionAsync(record);
            await Task.Delay(600);
            VerifyGolfEvent(DefaultCommandEventTypes.GolfWin, Times.Once());

            // Duplicate/late completion of the finalized stroke must not fire again
            await golfService.ProcessCompletionAsync(record);
            VerifyGolfEvent(DefaultCommandEventTypes.GolfWin, Times.Once());

            // A new shot starts again at stroke 1 (fresh hole at the tee)
            var token = await golfService.TriggerGolfShotAsync(args, power: 40.0, conditionOverride: GolfGreenCondition.StandardGreen, targetDistanceOverride: 84.0);
            Assert.False(string.IsNullOrWhiteSpace(token));
        }

        [Fact]
        public async Task GolfGameService_MultiStroke_FreePlay_FiresDeferredOutcomeAfterIdleTimeout()
        {
            var args = CreateGolfArgs();
            var firstDropToken = "";
            var golfService = await CreateMultiStrokeGolfServiceAsync(3, args, token => firstDropToken = token);
            golfService.FreePlayRoundIdleTimeout = TimeSpan.FromMilliseconds(100);

            await golfService.ProcessCompletionAsync(CreateGolfOutcomeRecord(args, strokeNumber: 1, isWin: true, dropToken: firstDropToken));
            VerifyGolfEvent(DefaultCommandEventTypes.GolfWin, Times.Never());

            for (var i = 0; i < 40; i++)
            {
                await Task.Delay(50);
                try
                {
                    VerifyGolfEvent(DefaultCommandEventTypes.GolfWin, Times.Once());
                    return;
                }
                catch (MockException)
                {
                    // keep waiting
                }
            }

            VerifyGolfEvent(DefaultCommandEventTypes.GolfWin, Times.Once());
        }

        [Fact]
        public async Task GolfGameService_MultiStroke_FreePlay_FollowUpShotStartCancelsIdleOutcomeOfEarlierStroke()
        {
            var args = CreateGolfArgs();
            var firstDropToken = "";
            var golfService = await CreateMultiStrokeGolfServiceAsync(3, args, token => firstDropToken = token);
            golfService.FreePlayRoundIdleTimeout = TimeSpan.FromMilliseconds(250);

            await golfService.ProcessCompletionAsync(CreateGolfOutcomeRecord(args, strokeNumber: 1, isWin: true, dropToken: firstDropToken));
            // Starting stroke 2 must invalidate stroke 1's idle timer before stroke 2 completes.
            var secondDropToken = await golfService.TriggerGolfShotAsync(args, power: 40.0, conditionOverride: GolfGreenCondition.StandardGreen, targetDistanceOverride: 84.0);

            await Task.Delay(350);
            VerifyGolfEvent(DefaultCommandEventTypes.GolfWin, Times.Never());
            VerifyGolfEvent(DefaultCommandEventTypes.GolfLose, Times.Never());

            // Once stroke 2 completes, its unchanged deferred outcome still finalizes after idle.
            await golfService.ProcessCompletionAsync(CreateGolfOutcomeRecord(args, strokeNumber: 2, isWin: false, dropToken: secondDropToken));
            await Task.Delay(700);
            VerifyGolfEvent(DefaultCommandEventTypes.GolfWin, Times.Never());
            VerifyGolfEvent(DefaultCommandEventTypes.GolfLose, Times.Once());
        }

        [Fact]
        public async Task GolfGameService_MultiStroke_SessionStop_FlushesDeferredOutcome()
        {
            var args = CreateGolfArgs();
            var golfService = await CreateMultiStrokeGolfServiceAsync(3, args);

            await golfService.StartSessionAsync(60, triggeredBy: "test");
            // Re-register the player's state inside the session (session start clears round states)
            await golfService.TriggerGolfShotAsync(args, power: 40.0, conditionOverride: GolfGreenCondition.StandardGreen, targetDistanceOverride: 84.0);

            await golfService.ProcessCompletionAsync(CreateGolfOutcomeRecord(args, strokeNumber: 1, isWin: true));
            VerifyGolfEvent(DefaultCommandEventTypes.GolfWin, Times.Never());

            // No idle timer in session mode: still nothing after a delay
            await Task.Delay(300);
            VerifyGolfEvent(DefaultCommandEventTypes.GolfWin, Times.Never());

            await golfService.StopSessionAsync("test");

            VerifyGolfEvent(DefaultCommandEventTypes.GolfWin, Times.Once());
        }

        [Fact]
        public async Task GolfGameService_MultiStrokeMode_ContinuesFromRestingBall_AndDeductsStrokePenalty()
        {
            var dropRegistry = CreateDropRegistry();
            var settingsService = CreateSettingsService();
            var settings = await settingsService.GetSettingsAsync();
            settings.Golf.MaxStrokesPerPlayer = 3;
            settings.Golf.StrokePenaltyPoints = 10;
            settings.Golf.PowerVariancePercent = 0.0;
            await settingsService.SaveSettingsAsync(settings);
            Assert.Equal(3, (await settingsService.GetSettingsAsync()).Golf.MaxStrokesPerPlayer);

            var golfService = CreateGolfService(dropRegistry, settingsService);

            var args = new CommandEventArgs
            {
                Command = "golf",
                Name = "golfer_multi",
                DisplayName = "GolferMulti",
                UserId = "golfer-1"
            };

            // Stroke 1: Tee shot with low power (40) that stops short of the cup
            var token1 = await golfService.TriggerGolfShotAsync(args, power: 40.0, conditionOverride: GolfGreenCondition.StandardGreen, targetDistanceOverride: 84.0);
            var record1 = dropRegistry.GetPendingDrop(token1);
            Assert.NotNull(record1);
            Assert.Equal(1, record1.StrokeNumber);
            Assert.Equal(0.05, record1.StartAxisPosition);
            Assert.False(record1.IsBullseye);

            // The authorized overlay reports where Stroke 1 came to rest; the server tracks that client result for Stroke 2.
            record1.LandingX = 0.40;
            await golfService.ProcessCompletionAsync(record1);
            dropRegistry.AcknowledgeCompletion(token1);

            // Stroke 2: Putt from resting position
            var token2 = await golfService.TriggerGolfShotAsync(args, power: 35.0);
            var record2 = dropRegistry.GetPendingDrop(token2);
            Assert.NotNull(record2);
            Assert.Equal(2, record2.StrokeNumber);
            Assert.Equal(0.40, record2.StartAxisPosition, 3);
        }

        [Fact]
        public async Task DropGameCompleteHandler_Golf_Authorized_AppliesReportedOutcomeAndStrokePenalty()
        {
            var dropRegistry = CreateDropRegistry();
            var settingsService = CreateSettingsService();
            var parachuteServiceMock = new Mock<IParachuteDropService>();
            var plinkoServiceMock = new Mock<IPlinkoDropService>();
            var balloonServiceMock = new Mock<IBalloonAscentService>();
            var curlingServiceMock = new Mock<ICurlingGameService>();
            var golfServiceMock = new Mock<IGolfGameService>();
            var coordinatorMock = new Mock<IFeatureRuntimeCoordinator>();
            coordinatorMock.Setup(c => c.IsEnabled(FeatureKeys.DropGames)).Returns(true);
            var securityService = CreateOverlaySecurityService();
            var validKey = await securityService.GetOverlayKeyAsync();

            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = "golf-auth-token",
                GameType = DropGameType.Golf,
                Username = "golfer_1",
                DisplayName = "Golfer1",
                UserId = "user-golf-1",
                StrokeNumber = 2,
                StrokePenalty = 10,
                EventArgs = new CommandEventArgs { Command = "golf", Name = "golfer_1", DisplayName = "Golfer1", UserId = "user-golf-1" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now.AddSeconds(-6),
                EarliestCompletionUtc = now.AddSeconds(-1),
                ExpiresAtUtc = now.AddSeconds(60)
            };

            dropRegistry.RegisterDrop(record);

            var handler = new DropGameCompleteHandler(
                dropRegistry,
                parachuteServiceMock.Object,
                plinkoServiceMock.Object,
                settingsService,
                securityService,
                coordinatorMock.Object,
                balloonServiceMock.Object,
                curlingServiceMock.Object,
                golfServiceMock.Object);

            // Authorized client reports a configurable score above the former 100-point server clamp.
            var notification = new DropGameCompleteNotification(
                "golf-auth-token",
                landingX: 0.840,
                score: 240,
                isBullseye: true,
                isWin: true,
                isHit: true,
                overlayKey: validKey,
                strokeNumber: 2,
                baseScore: 250,
                strokePenalty: 10,
                isLipOut: false);

            await handler.Handle(notification, CancellationToken.None);

            golfServiceMock.Verify(s => s.ProcessCompletionAsync(It.Is<PendingDropRecord>(r =>
                r.DropToken == "golf-auth-token" &&
                r.BaseScore == 250 &&
                r.StrokePenalty == 10 &&
                r.Score == 240 &&
                r.IsBullseye == true &&
                r.IsWin == true)), Times.Once);
        }

        [Fact]
        public async Task GolfGameService_ShotWithoutEmoteOrCustomImage_DoesNotFetchEmotes_AndUsesProceduralFallback()
        {
            var freshEmoteMock = new Mock<IEmoteService>();
            var dropRegistry = CreateDropRegistry();
            var settingsService = CreateSettingsService();
            var golfService = new GolfGameService(
                _serviceBackbone.Object,
                _commandHandler.Object,
                _defaultCommandTriggerService.Object,
                _scopeFactory.Object,
                freshEmoteMock.Object,
                _webSocketMessenger.Object,
                _dispatcher.Object,
                settingsService,
                dropRegistry,
                NullLogger<GolfGameService>.Instance);

            // Viewer types "!golf 75" (numeric power only, no emote, no custom theme image)
            var args = new CommandEventArgs
            {
                Command = "golf",
                Name = "golfer_no_emote",
                DisplayName = "GolferNoEmote",
                UserId = "user-no-emote-1",
                Args = ["75"],
                Arg = "75"
            };

            var token = await golfService.TriggerGolfShotAsync(args, allowDirectUrlOrPath: false);
            Assert.False(string.IsNullOrWhiteSpace(token));

            // Should NEVER call GetEmotesAsync or ResolveEmoteUrlAsync when no emote is provided and token is numeric power
            freshEmoteMock.Verify(e => e.GetEmotesAsync(), Times.Never);
            freshEmoteMock.Verify(e => e.ResolveEmoteUrlAsync(It.IsAny<string>()), Times.Never);

            // Should broadcast golf_shot with empty decalUrl so the overlay renders the procedural SVG/Canvas golfer
            _webSocketMessenger.Verify(
                w => w.AddToQueue(
                    WsTopics.Golf,
                    It.Is<string>(json => json.Contains("\"type\":\"golf_shot\"") && json.Contains("\"decalUrl\":\"\""))),
                Times.Once);
        }

        [Fact]
        public async Task CurlingGameService_StartSessionAndSlide_IncludesPowerVariancePercentInPayload()
        {
            var dropRegistry = CreateDropRegistry();
            var settingsService = CreateSettingsService();
            var settings = await settingsService.GetSettingsAsync();
            settings.Curling.PowerVariancePercent = 4.5;
            await settingsService.SaveSettingsAsync(settings);

            var curlingService = CreateCurlingService(dropRegistry, settingsService);

            // 1. Session start payload includes powerVariancePercent: 4.5
            var session = await curlingService.StartSessionAsync(60, triggeredBy: "ModUser");
            Assert.NotNull(session);

            _webSocketMessenger.Verify(
                w => w.AddToQueue(
                    WsTopics.Curling,
                    It.Is<string>(json => json.Contains("\"type\":\"session_start\"") && json.Contains("\"powerVariancePercent\":4.5"))),
                Times.Once);

            // 2. Curling slide payload includes powerVariancePercent: 4.5
            var args = new CommandEventArgs
            {
                Command = "curl",
                Name = "curler_variance_test",
                DisplayName = "CurlerVarianceTest",
                UserId = "user-curl-var-1",
                Args = ["80"]
            };

            var token = await curlingService.TriggerCurlingSlideAsync(args, power: 80.0);
            Assert.False(string.IsNullOrWhiteSpace(token));

            _webSocketMessenger.Verify(
                w => w.AddToQueue(
                    WsTopics.Curling,
                    It.Is<string>(json => json.Contains("\"type\":\"curling_slide\"") && json.Contains("\"powerVariancePercent\":4.5"))),
                Times.Once);
        }
    }
}

