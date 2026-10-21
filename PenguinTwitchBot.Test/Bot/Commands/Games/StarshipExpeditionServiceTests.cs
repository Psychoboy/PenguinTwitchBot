using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;

namespace PenguinTwitchBot.Test.Bot.Commands.Games;

public class StarshipExpeditionServiceTests
{
    [Fact]
    public async Task StartExpedition_OpensBoardingAndRejectsSecondActiveVoyage()
    {
        var stateStore = new InMemoryStateStore();
        var service = CreateService(stateStore);

        var snapshot = await service.StartExpeditionAsync("mod-user", legCount: 3, seed: 12345);

        Assert.Equal(12345, snapshot.Seed);
        Assert.Equal(3, snapshot.LegCount);
        Assert.Equal(StarshipExpeditionStatus.Boarding, snapshot.Status);
        Assert.Null(snapshot.CurrentEvent);
        Assert.NotNull(snapshot.BoardingClosesAtUtc);
        Assert.Equal(1, snapshot.NextEventSequence);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartExpeditionAsync("another-mod", seed: 99));
    }

    [Fact]
    public async Task StartAndTakeoff_AnnounceJoinAndVoteInstructionsAndFireTriggers()
    {
        var chatMessages = new List<string>();
        var serviceBackbone = new Mock<IServiceBackbone>();
        serviceBackbone.Setup(backbone => backbone.SendChatMessage(It.IsAny<string>()))
            .Callback<string>(chatMessages.Add)
            .Returns(Task.CompletedTask);
        var triggerService = new Mock<IDefaultCommandTriggerService>();
        triggerService.Setup(trigger => trigger.TriggerDefaultCommandEventAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CommandEventArgs>(), It.IsAny<Dictionary<string, string>>()))
            .Returns(Task.CompletedTask);
        var settingsStore = new InMemorySettingsStore(new StarshipExpeditionSettings { BoardingDurationSeconds = 30 });
        var service = CreateService(new InMemoryStateStore(), settingsStore: settingsStore,
            serviceBackboneMock: serviceBackbone, defaultCommandTriggerService: triggerService.Object);

        await service.StartExpeditionAsync("moderator", seed: 83);
        Assert.Contains(chatMessages, message => message.Contains("30s", StringComparison.Ordinal) && message.Contains("!starship join", StringComparison.Ordinal));
        Assert.DoesNotContain(chatMessages, message => message.Contains("!starship vote", StringComparison.Ordinal));

        await service.JoinCrewAsync(CreateArgs("viewer-1", "Pilot"));
        await service.AdvanceEventAsync();

        Assert.Contains(chatMessages, message => message.Contains("!starship vote <choice>", StringComparison.Ordinal));
        Assert.Contains(chatMessages, message => message.Contains("direct:", StringComparison.Ordinal));
        Assert.Contains(chatMessages, message => message.Contains("detour:", StringComparison.Ordinal));
        triggerService.Verify(trigger => trigger.TriggerDefaultCommandEventAsync(
            StarshipExpeditionService.DefaultCommandName,
            DefaultCommandEventTypes.StarshipBoardingOpened,
            It.IsAny<CommandEventArgs>(),
            It.IsAny<Dictionary<string, string>>()), Times.Once);
        triggerService.Verify(trigger => trigger.TriggerDefaultCommandEventAsync(
            StarshipExpeditionService.DefaultCommandName,
            DefaultCommandEventTypes.StarshipEventOpened,
            It.IsAny<CommandEventArgs>(),
            It.IsAny<Dictionary<string, string>>()), Times.Once);
    }

    [Fact]
    public async Task BoardingTimeout_AbortsWithoutCrewAndLateJoinersCannotVote()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var settingsStore = new InMemorySettingsStore(new StarshipExpeditionSettings { BoardingDurationSeconds = 20 });
        var service = CreateService(new InMemoryStateStore(), clock, settingsStore);
        await service.StartExpeditionAsync("moderator", seed: 84);

        clock.Advance(TimeSpan.FromSeconds(21));
        await service.ProcessDueTransitionAsync();
        Assert.Equal(StarshipExpeditionStatus.Aborted, (await service.GetSnapshotAsync())?.Status);

        var nextVoyage = await service.StartExpeditionAsync("moderator", seed: 85);
        await service.JoinCrewAsync(CreateArgs("viewer-1", "Pilot"));
        await service.AdvanceEventAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.JoinCrewAsync(CreateArgs("late-viewer", "Late Viewer")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.VoteAsync(CreateArgs("late-viewer", "Late Viewer"), "direct"));
        Assert.Equal(StarshipExpeditionStatus.EventOpen, (await service.GetSnapshotAsync())?.Status);
        Assert.NotEqual(Guid.Empty, nextVoyage.ExpeditionId);
    }

    [Fact]
    public async Task StartCommand_RequiresModeratorOrBroadcaster()
    {
        var viewerStore = new InMemoryStateStore();
        var viewerService = CreateService(viewerStore);
        await viewerService.OnCommand(null, new CommandEventArgs
        {
            Command = StarshipExpeditionService.DefaultCommandName,
            Name = "viewer",
            DisplayName = "Viewer",
            UserId = "viewer-1",
            Args = ["start"]
        });
        Assert.Null(await viewerStore.LoadAsync());

        var moderatorStore = new InMemoryStateStore();
        var moderatorService = CreateService(moderatorStore);
        await moderatorService.OnCommand(null, new CommandEventArgs
        {
            Command = StarshipExpeditionService.DefaultCommandName,
            Name = "moderator",
            DisplayName = "Moderator",
            UserId = "moderator-1",
            IsMod = true,
            Args = ["start", "3", "15", "20"]
        });

        var started = await moderatorStore.LoadAsync();
        Assert.NotNull(started);
        Assert.Equal(3, started.LegCount);
        Assert.Equal(900, started.MinimumEventIntervalSeconds);
        Assert.Equal(1200, started.MaximumEventIntervalSeconds);
        Assert.Equal(StarshipExpeditionStatus.Boarding, started.Status);
    }

    [Fact]
    public async Task StopAndAdvanceCommands_RequireModeratorOrBroadcaster()
    {
        var store = new InMemoryStateStore();
        var service = CreateService(store);
        await service.StartExpeditionAsync("moderator", legCount: 3, seed: 125);

        await service.OnCommand(null, new CommandEventArgs
        {
            Command = StarshipExpeditionService.DefaultCommandName,
            Name = "viewer",
            DisplayName = "Viewer",
            UserId = "viewer-1",
            Args = ["stop"]
        });
        var afterViewerStop = await service.GetSnapshotAsync();
        Assert.Equal(StarshipExpeditionStatus.Boarding, afterViewerStop?.Status);

        await service.OnCommand(null, new CommandEventArgs
        {
            Command = StarshipExpeditionService.DefaultCommandName,
            Name = "viewer",
            DisplayName = "Viewer",
            UserId = "viewer-1",
            Args = ["advance"]
        });
        var afterViewerAdvance = await service.GetSnapshotAsync();
        Assert.Equal(StarshipExpeditionStatus.Boarding, afterViewerAdvance?.Status);
        Assert.Null(afterViewerAdvance?.CurrentEvent);

        await service.OnCommand(null, new CommandEventArgs
        {
            Command = StarshipExpeditionService.DefaultCommandName,
            Name = "moderator",
            DisplayName = "Moderator",
            UserId = "moderator-1",
            IsMod = true,
            Args = ["stop"]
        });
        Assert.Equal(StarshipExpeditionStatus.Aborted, (await store.LoadAsync())?.Status);
    }

    [Fact]
    public async Task Vote_RequiresCrewEnrollmentAndRejectsSecondVoteFromSameViewer()
    {
        var service = CreateService(new InMemoryStateStore());
        await service.StartExpeditionAsync("mod-user", legCount: 3, seed: 12345);
        var voter = CreateArgs("viewer-1", "Pilot");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.VoteAsync(voter, "detour"));
        await service.JoinCrewAsync(voter);
        await service.AdvanceEventAsync();

        await service.VoteAsync(voter, "detour");
        var duplicateVote = await Assert.ThrowsAsync<InvalidOperationException>(() => service.VoteAsync(voter, "direct"));
        Assert.Contains("already been recorded", duplicateVote.Message);

        var snapshot = await service.GetSnapshotAsync();
        Assert.NotNull(snapshot);
        var crewMember = Assert.Single(snapshot.Crew);
        Assert.Equal("viewer-1", crewMember.UserId);
        Assert.Equal(StarshipCrewEnrollmentMethod.ExplicitJoin, crewMember.EnrollmentMethod);
        Assert.Equal("detour", Assert.Single(snapshot.CurrentEvent!.Votes).ChoiceId);
    }

    [Fact]
    public async Task NewService_RestoresOpenExpeditionAndRecordedVote()
    {
        var stateStore = new InMemoryStateStore();
        var originalService = CreateService(stateStore);
        var started = await originalService.StartExpeditionAsync("mod-user", legCount: 4, seed: 642);
        await originalService.JoinCrewAsync(CreateArgs("viewer-1", "Pilot"));
        await originalService.AdvanceEventAsync();
        await originalService.VoteAsync(CreateArgs("viewer-1", "Pilot"), "detour");

        var restored = await CreateService(stateStore).GetSnapshotAsync();

        Assert.NotNull(restored);
        Assert.Equal(started.ExpeditionId, restored.ExpeditionId);
        Assert.Equal(StarshipExpeditionStatus.EventOpen, restored.Status);
        Assert.Equal("viewer-1", Assert.Single(restored.CurrentEvent!.Votes).UserId);
        Assert.Equal("detour", restored.CurrentEvent.Votes[0].ChoiceId);
    }

    [Fact]
    public async Task Vote_PersistenceFailureRollsBackMemoryAndAllowsRetry()
    {
        var stateStore = new InMemoryStateStore();
        var service = CreateService(stateStore);
        await service.StartExpeditionAsync("mod-user", legCount: 3, seed: 642);
        var voter = CreateArgs("viewer-1", "Pilot");
        await service.JoinCrewAsync(voter);
        await service.AdvanceEventAsync();
        stateStore.FailNextSave = true;

        await Assert.ThrowsAsync<IOException>(() => service.VoteAsync(voter, "detour"));

        var afterFailure = await service.GetSnapshotAsync();
        Assert.NotNull(afterFailure);
        Assert.Single(afterFailure.Crew);
        Assert.Empty(afterFailure.CurrentEvent!.Votes);

        await service.VoteAsync(voter, "detour");
        var afterRetry = await service.GetSnapshotAsync();
        Assert.Equal("viewer-1", Assert.Single(afterRetry!.Crew).UserId);
        Assert.Equal("detour", Assert.Single(afterRetry.CurrentEvent!.Votes).ChoiceId);
    }

    [Fact]
    public async Task AdvanceEvent_UsesDefaultForNoVotesAndTie_ThenSchedulesNextEventInConfiguredRange()
    {
        var service = CreateService(new InMemoryStateStore());
        await service.StartExpeditionAsync("mod-user", legCount: 4, minimumEventIntervalMinutes: 15, maximumEventIntervalMinutes: 30, seed: 2468);
        await service.JoinCrewAsync(CreateArgs("viewer-1", "Pilot"));
        await service.JoinCrewAsync(CreateArgs("viewer-2", "Engineer"));
        await service.AdvanceEventAsync();

        var noVoteResolution = await service.AdvanceEventAsync();

        Assert.Equal("Direct Route", noVoteResolution.CurrentRoute);
        Assert.Equal(1, noVoteResolution.CurrentLeg);
        Assert.Equal(StarshipExpeditionStatus.Cruising, noVoteResolution.Status);
        Assert.InRange(
            (noVoteResolution.NextEventAtUtc!.Value - noVoteResolution.UpdatedAtUtc).TotalSeconds,
            900,
            1800);

        var nextEvent = await service.AdvanceEventAsync();
        Assert.NotEqual("SalvageSite", nextEvent.CurrentEvent!.EventType);
        var choices = nextEvent.CurrentEvent!.Choices;
        await service.VoteAsync(CreateArgs("viewer-1", "Pilot"), choices[0].ChoiceId);
        await service.VoteAsync(CreateArgs("viewer-2", "Engineer"), choices[1].ChoiceId);
        var tieResolution = await service.AdvanceEventAsync();

        Assert.Equal(nextEvent.CurrentEvent.DefaultChoiceId, tieResolution.CompletedEvents.Last().ResolvedChoiceId);
        Assert.Equal(2, tieResolution.CurrentLeg);
    }

    [Fact]
    public async Task SurveyRoute_BranchesEventPoolAndAdvancesSector()
    {
        var service = CreateService(new InMemoryStateStore());
        await service.StartExpeditionAsync("mod-user", legCount: 4, seed: 13579);
        await service.JoinCrewAsync(CreateArgs("viewer-1", "Pilot"));
        var started = await service.AdvanceEventAsync();
        var startingSector = started.CurrentSector;

        await service.VoteAsync(CreateArgs("viewer-1", "Pilot"), "detour");
        var routeResolution = await service.AdvanceEventAsync();
        Assert.Equal("Survey Route", routeResolution.CurrentRoute);
        Assert.NotEqual(startingSector, routeResolution.CurrentSector);

        var surveyEvent = await service.AdvanceEventAsync();
        Assert.Equal("Survey Route", surveyEvent.CurrentRoute);
        Assert.NotEqual("Hazard", surveyEvent.CurrentEvent?.EventType);
    }

    [Fact]
    public async Task StationStop_IsInsertedAtConfiguredLegCadence()
    {
        var service = CreateService(new InMemoryStateStore());
        await service.StartExpeditionAsync("mod-user", legCount: 5, seed: 789);
        await service.JoinCrewAsync(CreateArgs("viewer-1", "Pilot"));
        await service.AdvanceEventAsync();

        await service.AdvanceEventAsync();
        await service.AdvanceEventAsync();
        await service.AdvanceEventAsync();
        var stationEvent = await service.AdvanceEventAsync();

        Assert.Equal(StarshipExpeditionStatus.EventOpen, stationEvent.Status);
        Assert.Equal("StationStop", stationEvent.CurrentEvent?.EventType);
        Assert.Contains(stationEvent.CurrentEvent!.Choices, choice => choice.ChoiceId == "resupply");
    }

    [Fact]
    public async Task StopExpedition_AbortsActiveVoyageAndPersistsTerminalState()
    {
        var stateStore = new InMemoryStateStore();
        var service = CreateService(stateStore);
        var started = await service.StartExpeditionAsync("mod-user", seed: 101);

        var stopped = await service.StopExpeditionAsync("mod-user");

        Assert.NotNull(stopped);
        Assert.Equal(StarshipExpeditionStatus.Aborted, stopped.Status);
        Assert.Null(stopped.CurrentEvent);
        Assert.Equal(StarshipExpeditionStatus.Aborted, (await stateStore.LoadAsync())?.Status);
        Assert.Equal(started.ExpeditionId, stopped.ExpeditionId);
    }

    [Fact]
    public async Task DueEventTimeout_ResolvesDefaultAndDoesNotRepeatAfterReload()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var stateStore = new InMemoryStateStore();
        var service = CreateService(stateStore, clock);
        await service.StartExpeditionAsync("mod-user", legCount: 3, seed: 20261020);
        await service.JoinCrewAsync(CreateArgs("viewer-1", "Pilot"));
        await service.AdvanceEventAsync();

        clock.Advance(TimeSpan.FromSeconds(61));
        await service.ProcessDueTransitionAsync();
        var afterTimeout = await service.GetSnapshotAsync();

        Assert.NotNull(afterTimeout);
        Assert.Equal(StarshipExpeditionStatus.Cruising, afterTimeout.Status);
        Assert.Equal(1, afterTimeout.CurrentLeg);
        Assert.Null(afterTimeout.CurrentEvent);
        Assert.Equal("direct", Assert.Single(afterTimeout.CompletedEvents).ResolvedChoiceId);

        await service.ProcessDueTransitionAsync();
        var afterSecondTick = await CreateService(stateStore, clock).GetSnapshotAsync();
        Assert.Single(afterSecondTick!.CompletedEvents);
        Assert.Equal(1, afterSecondTick.CurrentLeg);
    }

    [Fact]
    public async Task BuildPreview_UsesCustomSectorPoolDeterministicallyWithoutTouchingLiveState()
    {
        var stateStore = new InMemoryStateStore();
        var settingsStore = new InMemorySettingsStore();
        var service = CreateService(stateStore, settingsStore: settingsStore);
        var settings = new StarshipExpeditionSettings
        {
            LegCount = 8,
            CustomSectors = ["Cerulean Gate", "Echo Reach"]
        };

        var firstPreview = service.BuildPreview(settings, seed: 881122);
        var secondPreview = service.BuildPreview(settings, seed: 881122);

        Assert.Equal(firstPreview.Select(item => item.EventId), secondPreview.Select(item => item.EventId));
        Assert.Equal(firstPreview.Select(item => item.Sector), secondPreview.Select(item => item.Sector));
        Assert.Contains(firstPreview, item => item.Sector is "Cerulean Gate" or "Echo Reach");
        Assert.Equal(8, firstPreview.Count);
        Assert.Null(await stateStore.LoadAsync());
        Assert.Equal(0, settingsStore.SaveCount);
    }

    [Fact]
    public async Task StartExpedition_SnapshotsSettingsAndSectorPoolForCurrentVoyage()
    {
        var settingsStore = new InMemorySettingsStore(new StarshipExpeditionSettings
        {
            LegCount = 6,
            StationIntervalLegs = 3,
            ActiveCrewRewardsOnly = false,
            CustomSectors = ["Cerulean Gate"]
        });
        var service = CreateService(new InMemoryStateStore(), settingsStore: settingsStore);

        var started = await service.StartExpeditionAsync("moderator", seed: 444);
        settingsStore.Settings.CustomSectors.Add("Changed After Start");
        var current = await service.GetSnapshotAsync();

        Assert.Equal(6, current!.LegCount);
        Assert.Equal(3, current.StationIntervalLegs);
        Assert.False(current.ActiveCrewRewardsOnly);
        Assert.Contains("Cerulean Gate", current.SectorPool);
        Assert.DoesNotContain("Changed After Start", current.SectorPool);
        Assert.Equal(started.ExpeditionId, current.ExpeditionId);
    }

    private static StarshipExpeditionService CreateService(
        InMemoryStateStore stateStore,
        TimeProvider? timeProvider = null,
        InMemorySettingsStore? settingsStore = null,
        Mock<IServiceBackbone>? serviceBackboneMock = null,
        IDefaultCommandTriggerService? defaultCommandTriggerService = null)
    {
        var serviceBackbone = serviceBackboneMock ?? new Mock<IServiceBackbone>();
        if (serviceBackboneMock == null)
            serviceBackbone.Setup(backbone => backbone.SendChatMessage(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
        serviceBackbone.Setup(backbone => backbone.SendChatMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);
        var webSocketMessenger = new Mock<IWebSocketMessenger>();
        webSocketMessenger.Setup(messenger => messenger.AddToQueue(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        return new StarshipExpeditionService(
            serviceBackbone.Object,
            Mock.Of<ICommandHandler>(),
            stateStore,
            settingsStore ?? new InMemorySettingsStore(),
            webSocketMessenger.Object,
            NullLogger<StarshipExpeditionService>.Instance,
            Mock.Of<IPenguinDispatcher>(),
            timeProvider,
            defaultCommandTriggerService);
    }

    private static CommandEventArgs CreateArgs(string userId, string displayName) => new()
    {
        Command = StarshipExpeditionService.DefaultCommandName,
        Name = displayName.ToLowerInvariant(),
        DisplayName = displayName,
        UserId = userId
    };

    private sealed class InMemoryStateStore : IStarshipExpeditionStateStore
    {
        private string? _snapshotJson;
        public bool FailNextSave { get; set; }

        public Task<StarshipExpeditionSnapshot?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_snapshotJson == null
                ? null
                : JsonSerializer.Deserialize<StarshipExpeditionSnapshot>(_snapshotJson));

        public Task SaveAsync(StarshipExpeditionSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            if (FailNextSave)
            {
                FailNextSave = false;
                throw new IOException("Simulated state persistence failure.");
            }

            _snapshotJson = JsonSerializer.Serialize(snapshot);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySettingsStore : IStarshipExpeditionSettingsStore
    {
        public int SaveCount { get; private set; }
        public StarshipExpeditionSettings Settings { get; private set; }

        public InMemorySettingsStore(StarshipExpeditionSettings? settings = null)
        {
            Settings = settings ?? new StarshipExpeditionSettings();
        }

        public Task<StarshipExpeditionSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Settings);

        public Task SaveAsync(StarshipExpeditionSettings settings, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            Settings = settings;
            return Task.CompletedTask;
        }
    }
}
