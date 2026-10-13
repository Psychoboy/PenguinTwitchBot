using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Models.Commands;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Database.Bot.Models;
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

        private DropGamesService CreateService()
        {
            return new DropGamesService(
                _serviceBackbone.Object,
                _commandHandler.Object,
                _defaultCommandTriggerService.Object,
                _scopeFactory.Object,
                _emoteService.Object,
                _webSocketMessenger.Object,
                _dispatcher.Object,
                NullLogger<DropGamesService>.Instance);
        }

        [Fact]
        public async Task GetSettingsAsync_InitializesFourStockSeasonalThemesAndDefaultPlinkoBuckets()
        {
            var sut = CreateService();

            var settings = await sut.GetSettingsAsync();

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
            var sut = CreateService();
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
            var resolvedNative = await sut.ResolveCharacterOrEmoteUrlAsync(nativeEventArgs, null, "/fallback.png");
            Assert.Equal("https://static-cdn.jtvnw.net/emoticons/v2/12345/default/dark/3.0", resolvedNative);

            // 2. BTTV/FFZ/7TV/Channel emote code resolved via IEmoteService
            var bttvEventArgs = new CommandEventArgs
            {
                Name = "viewer1",
                DisplayName = "Viewer1",
                Arg = "penguindance extra text"
            };
            var resolvedEmote = await sut.ResolveCharacterOrEmoteUrlAsync(bttvEventArgs, null, "/fallback.png");
            Assert.Equal("https://cdn.7tv.app/emote/penguin_dance/3x.webp", resolvedEmote);

            // 3. Direct custom URL is preserved
            var resolvedDirect = await sut.ResolveCharacterOrEmoteUrlAsync(null, "/media/custom_chibi.png", "/fallback.png");
            Assert.Equal("/media/custom_chibi.png", resolvedDirect);

            // 4. Unknown word or empty input falls back to theme default character sprite
            var noEmoteArgs = new CommandEventArgs
            {
                Name = "viewer1",
                DisplayName = "Viewer1",
                Arg = "NotAnEmote"
            };
            var resolvedFallback = await sut.ResolveCharacterOrEmoteUrlAsync(noEmoteArgs, null, "/themes/spring_default.png");
            Assert.Equal("/themes/spring_default.png", resolvedFallback);
        }

        [Fact]
        public async Task TriggerParachuteDropAsync_CreatesAuthoritativeDropAndQueuesOnParachuteTopic()
        {
            var sut = CreateService();

            var token = await sut.TriggerParachuteDropAsync(
                new CommandEventArgs { Name = "testuser", DisplayName = "TestUser", UserId = "1001" },
                emoteOrImage: null,
                themeOverrideId: "winter",
                forcedStartX: 0.50,
                forcedWindAccelX: 0.0);

            Assert.False(string.IsNullOrWhiteSpace(token));
            Assert.Equal(64, token.Length); // 256-bit hex token

            var pending = sut.GetPendingDrop(token);
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
            var sut = CreateService();

            // Start a parachute drop aimed dead center (bullseye)
            var token = await sut.TriggerParachuteDropAsync(
                new CommandEventArgs { Name = "parachuter", DisplayName = "Parachuter", UserId = "2002" },
                emoteOrImage: null,
                themeOverrideId: "spring",
                forcedStartX: 0.50,
                forcedWindAccelX: 0.0);

            var before = sut.GetPendingDrop(token)!;
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

            var after = sut.GetPendingDrop(token)!;
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
            var sut = CreateService();
            var settings = await sut.GetSettingsAsync();
            settings.Parachute.WinActionName = "ParachuteWinnerReward";
            settings.Parachute.AnnounceWinsInChat = false;
            await sut.SaveSettingsAsync(settings);

            var rewardAction = new ActionType { Name = "ParachuteWinnerReward", Enabled = true };
            _actionManagementService.Setup(x => x.GetAllActionsAsync()).ReturnsAsync([rewardAction]);

            // 1. Unknown/forged token is rejected
            var forgedResult = await sut.CompleteDropAsync("0000000000000000000000000000000000000000000000000000000000000000");
            Assert.False(forgedResult);
            _actionService.Verify(x => x.EnqueueAction(It.IsAny<ConcurrentDictionary<string, string>>(), It.IsAny<ActionType>()), Times.Never);

            // 2. Premature completion before EarliestCompletionUtc is rejected
            var prematureToken = await sut.TriggerParachuteDropAsync(
                new CommandEventArgs { Name = "earlyuser", DisplayName = "EarlyUser", UserId = "3002" },
                forcedStartX: 0.50,
                forcedWindAccelX: 0.0,
                skipMinimumDurationCheck: false);

            var prematureResult = await sut.CompleteDropAsync(prematureToken);
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

            var validResult = await sut.CompleteDropAsync(validToken);
            Assert.True(validResult);

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
            var replayResult = await sut.CompleteDropAsync(validToken);
            Assert.False(replayResult);
            _actionService.Verify(x => x.EnqueueAction(It.IsAny<ConcurrentDictionary<string, string>>(), It.IsAny<ActionType>()), Times.Once);
        }

        [Fact]
        public async Task TriggerPlinkoDropAsync_SelectsWinningBucket_AndCompletesSecurely()
        {
            var sut = CreateService();
            var settings = await sut.GetSettingsAsync();
            settings.Plinko.WinActionName = "PlinkoJackpotAction";
            settings.Plinko.AnnounceWinsInChat = false;
            await sut.SaveSettingsAsync(settings);

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

            var pending = sut.GetPendingDrop(token);
            Assert.NotNull(pending);
            Assert.Equal(DropGameType.Plinko, pending.GameType);
            Assert.True(pending.IsWin);

            var completed = await sut.CompleteDropAsync(token);
            Assert.True(completed);

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

            var sut1 = CreateService();
            var settings = await sut1.GetSettingsAsync();
            foreach (var b in settings.Plinko.Buckets)
            {
                b.IsWin = b.Label == "JACKPOT";
            }
            settings.Plinko.WinActionName = "PlinkoJackpotAction";
            settings.Plinko.AnnounceWinsInChat = true;
            await sut1.SaveSettingsAsync(settings);

            Assert.NotNull(savedSetting);

            // Simulate reloading from DB in a fresh service instance
            _settingsRepo.Setup(x => x.GetAsync(
                It.IsAny<Expression<Func<Setting, bool>>>(),
                It.IsAny<Func<IQueryable<Setting>, IOrderedQueryable<Setting>>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string>()))
                .ReturnsAsync(() => savedSetting != null ? [savedSetting] : []);

            var sut2 = CreateService();
            var reloaded = await sut2.GetSettingsAsync();
            Assert.Equal(7, reloaded.Plinko.Buckets.Count);
            var singleWin = Assert.Single(reloaded.Plinko.Buckets, b => b.IsWin);
            Assert.Equal("JACKPOT", singleWin.Label);

            // Force landing in index 1 ("50 PTS", where IsWin == false)
            var token = await sut2.TriggerPlinkoDropAsync(
                new CommandEventArgs { Name = "viewer2", DisplayName = "Viewer2", UserId = "5005" },
                forcedBucketIndex: 1,
                skipMinimumDurationCheck: true);

            var pending = sut2.GetPendingDrop(token);
            Assert.NotNull(pending);
            Assert.False(pending.IsWin);
            Assert.Equal(0, pending.Score);

            var completed = await sut2.CompleteDropAsync(token);
            Assert.True(completed);

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
            var sut = CreateService();

            var startDto = await sut.StartSessionAsync(DropGameType.Parachute, 90, triggeredBy: "Streamer");
            Assert.True(startDto.IsActive);
            Assert.Equal(90, startDto.TotalDurationSeconds);
            Assert.Equal(0, startDto.TotalDrops);

            var current = sut.GetCurrentSession(DropGameType.Parachute);
            Assert.NotNull(current);
            Assert.True(current.IsActive);

            _webSocketMessenger.Verify(x => x.AddToQueue(
                WsTopics.Parachute,
                It.Is<string>(json => json.Contains("session_start") && json.Contains("\"totalDurationSeconds\":90"))),
                Times.Once);

            var stopDto = await sut.StopSessionAsync(DropGameType.Parachute, triggeredBy: "Streamer");
            Assert.NotNull(stopDto);
            Assert.False(stopDto.IsActive);

            var afterStop = sut.GetCurrentSession(DropGameType.Parachute);
            Assert.Null(afterStop);

            _webSocketMessenger.Verify(x => x.AddToQueue(
                WsTopics.Parachute,
                It.Is<string>(json => json.Contains("session_end"))),
                Times.Once);
        }

        [Fact]
        public async Task OnCommand_AutoStartsSession_WhenEnabled_AndEnforcesMaxDrops()
        {
            var sut = CreateService();
            var settings = await sut.GetSettingsAsync();
            settings.Parachute.SessionModeEnabled = true;
            settings.Parachute.AutoStartOnCommand = true;
            settings.Parachute.DefaultSessionDurationSeconds = 60;
            settings.Parachute.MaxDropsPerUserPerSession = 1;
            await sut.SaveSettingsAsync(settings);

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

            var session = sut.GetCurrentSession(DropGameType.Parachute);
            Assert.NotNull(session);
            Assert.True(session.IsActive);
            Assert.Equal(1, session.TotalDrops);

            // Second drop by same user should be blocked by MaxDropsPerUserPerSession limit
            await sut.OnCommand(this, e1);
            Assert.Equal(1, sut.GetCurrentSession(DropGameType.Parachute)!.TotalDrops);

            // Different user can drop
            var e2 = new CommandEventArgs
            {
                Command = "drop",
                Name = "viewer2",
                DisplayName = "Viewer2",
                UserId = "user-2"
            };
            await sut.OnCommand(this, e2);
            Assert.Equal(2, sut.GetCurrentSession(DropGameType.Parachute)!.TotalDrops);
        }

        [Fact]
        public async Task OnCommand_DoesNotAutoStart_WhenAutoStartOnCommandIsFalse()
        {
            var sut = CreateService();
            var settings = await sut.GetSettingsAsync();
            settings.Parachute.SessionModeEnabled = true;
            settings.Parachute.AutoStartOnCommand = false;
            await sut.SaveSettingsAsync(settings);

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

            var session = sut.GetCurrentSession(DropGameType.Parachute);
            Assert.Null(session);
            _webSocketMessenger.Verify(x => x.AddToQueue(WsTopics.Parachute, It.Is<string>(j => j.Contains("parachute_drop"))), Times.Never);
        }
    }
}
