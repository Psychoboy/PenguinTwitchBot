using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
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
    }
}
