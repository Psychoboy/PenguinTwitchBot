using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Database.Bot.Models.Points;

namespace PenguinTwitchBot.Test.Bot.Commands.Games;

public class StarshipExpeditionServiceTests
{
    [Fact]
    public void SharedOutcomeVariables_PreserveAggregateValuesWithoutScopeSpecificFields()
    {
        var expeditionEvent = new StarshipExpeditionEventSnapshot
        {
            EventType = "custom-cache", Title = "Cache", Subject = "the Archive", OutcomeResult = "Success", OutcomeRarity = "Rare",
            OutcomeStory = "The crew recovered the cache.", Path = [new() { Label = "Scan" }, new() { Label = "Recover" }],
            RewardOutcomes =
            [
                new() { UserId = "pilot", PointTypeId = 2, PointTypeName = "Credits", EarnedAmount = 10 },
                new() { UserId = "pilot", PointTypeId = 3, PointTypeName = "Tokens", EarnedAmount = 5, LostAmount = 2 }
            ]
        };
        var variables = StarshipEventVariables.ForOutcome(expeditionEvent);
        Assert.Equal("custom-cache", variables["EventType"]);
        Assert.Equal("15", variables["RewardTotal"]);
        Assert.Equal("2", variables["LostTotal"]);
        Assert.Equal("Credits", variables["PointType"]);
        Assert.Equal("Scan > Recover", variables["Path"]);
        Assert.Equal(expeditionEvent.OutcomeStory, variables["Story"]);
        Assert.False(variables.ContainsKey("AffectedCount"));
        Assert.False(variables.ContainsKey("Summary"));
        Assert.False(variables.ContainsKey("BankedTotal"));
    }

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

        Assert.Contains(chatMessages, message => message.Contains("!sv <number>", StringComparison.Ordinal));
        Assert.Contains(chatMessages, message => message.Contains("1) ", StringComparison.Ordinal));
        Assert.Contains(chatMessages, message => message.Contains("2) ", StringComparison.Ordinal));
        triggerService.Verify(trigger => trigger.TriggerDefaultCommandEventAsync(
            StarshipExpeditionService.DefaultCommandName,
            DefaultCommandEventTypes.StarshipBoardingOpened,
            It.IsAny<CommandEventArgs>(),
            It.Is<Dictionary<string, string>>(variables => variables["ShipSystemsEnabled"] == "true" && variables["Hull"] == "100" && variables["Supplies"] == "100")), Times.Once);
        triggerService.Verify(trigger => trigger.TriggerDefaultCommandEventAsync(
            StarshipExpeditionService.DefaultCommandName,
            DefaultCommandEventTypes.StarshipEventOpened,
            It.IsAny<CommandEventArgs>(),
            It.IsAny<Dictionary<string, string>>()), Times.Once);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task StatusCommand_IncludesShipResourcesOnlyForIntegratedVoyages(bool integrated, bool expectResources)
    {
        var snapshot = CreateSalvageFixture(shipSystems: integrated ? StarshipShipSystems.Create(new() { StartingHull = 72, StartingSupplies = 34 }) : null);
        var store = new InMemoryStateStore();
        await store.SaveAsync(snapshot);
        var replies = new List<string>();
        var backbone = new Mock<IServiceBackbone>();
        backbone.Setup(service => service.SendChatMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, string, bool>((_, message, _) => replies.Add(message))
            .Returns(Task.CompletedTask);
        var service = CreateService(store, serviceBackboneMock: backbone);
        var args = CreateArgs("viewer-id", "Viewer");
        args.Args = ["status"];

        await service.OnCommand(null, args);

        var reply = Assert.Single(replies);
        Assert.Contains("EventOpen, leg", reply);
        Assert.Equal(expectResources, reply.Contains("Hull: 72/100, Supplies: 34/100.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BoardingTimeout_AbortsWithoutCrewAndLateViewersAreEnrolledForTheNextEvent()
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
        var openEvent = (await service.GetSnapshotAsync())!.CurrentEvent!;

        var joined = await service.JoinCrewAsync(CreateArgs("late-viewer", "Late Viewer"));
        var lateJoiner = joined.Crew.Single(member => member.UserId == "late-viewer");
        Assert.Equal(StarshipCrewEnrollmentMethod.ExplicitJoin, lateJoiner.EnrollmentMethod);
        Assert.Equal(openEvent.Sequence + 1, lateJoiner.EffectiveFromSequence);

        var voted = await service.VoteAsync(CreateArgs("voter-id", "Voter"), "1");
        var autoEnrolled = voted.Crew.Single(member => member.UserId == "voter-id");
        Assert.Equal(StarshipCrewEnrollmentMethod.AutomaticAction, autoEnrolled.EnrollmentMethod);
        Assert.Equal(openEvent.Sequence + 1, autoEnrolled.EffectiveFromSequence);
        Assert.Contains(voted.CurrentEvent!.Votes, vote => vote.UserId == "voter-id");
        Assert.Equal(StarshipExpeditionStatus.EventOpen, (await service.GetSnapshotAsync())?.Status);
        Assert.NotEqual(Guid.Empty, nextVoyage.ExpeditionId);
    }

    [Fact]
    public async Task AutoEnrolledVoter_CountsInVoteButSharesRewardsOnlyFromNextEvent()
    {
        var fixture = CreateSalvageFixture();
        fixture.RewardProfiles[0].SuccessChancePercent = 100;
        var stateStore = new InMemoryStateStore();
        await stateStore.SaveAsync(fixture);
        var service = CreateService(stateStore, settingsStore: new InMemorySettingsStore(), pointsSystem: Mock.Of<IPointsSystem>());

        await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "salvage");
        await service.ConfirmRiskActionAsync(CreateArgs("pilot-id", "Pilot"));
        await service.VoteAsync(CreateArgs("newcomer-id", "Newcomer"), "salvage");
        await service.ConfirmRiskActionAsync(CreateArgs("newcomer-id", "Newcomer"));
        var voting = await service.GetSnapshotAsync();
        Assert.Equal(2, voting!.CurrentEvent!.Votes.Count);
        Assert.Contains(voting.Crew, member => member.UserId == "newcomer-id");

        var resolved = await service.AdvanceEventAsync();

        var completed = Assert.Single(resolved.CompletedEvents);
        Assert.Equal("salvage", completed.ResolvedChoiceId);
        Assert.Equal(["pilot-id"], completed.EligibleRewardUserIds);
        Assert.DoesNotContain(completed.RewardOutcomes, outcome => outcome.UserId == "newcomer-id" && (outcome.EarnedAmount > 0 || outcome.LostAmount > 0));
        Assert.Empty(resolved.Crew.Single(member => member.UserId == "newcomer-id").UnbankedBalances);
        Assert.Equal(110, resolved.Crew.Single(member => member.UserId == "pilot-id").UnbankedBalances.Single().Amount);
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
        Assert.Contains(firstPreview, item => item.ShipContext is not null);
        Assert.Contains(firstPreview, item => item.OutcomeResult is "Success" or "Failure");
        Assert.Null(await stateStore.LoadAsync());
        Assert.Equal(0, settingsStore.SaveCount);
    }

    [Fact]
    public void BuildPreview_ExposesEveryOptionAndGeneratedStoryForTheFullVoyage()
    {
        var service = CreateService(new InMemoryStateStore());
        var preview = service.BuildPreview(new StarshipExpeditionSettings { LegCount = 24 }, seed: 2024);

        var storyEvents = preview.Where(item => item.EventType is "SalvageSite" or "SignalDetected" or "Hazard").ToList();
        Assert.NotEmpty(storyEvents);
        Assert.All(storyEvents, item =>
        {
            Assert.NotEmpty(item.Subject);
            Assert.DoesNotContain("{", item.Description, StringComparison.Ordinal);
            Assert.True(item.ChoicePool.Count >= 3);
            Assert.All(item.ChoicePool, choice => Assert.NotEmpty(choice.Story));
            Assert.NotEmpty(item.SuccessStory);
        });
        Assert.All(preview.Where(item => item.EventType == "SalvageSite"), item =>
            Assert.Contains(item.ChoicePool, choice => choice.ContinuesEvent));
    }

    [Fact]
    public void BuildPreview_UsesOnlySelectedBuiltInsAndCustomSectorsDeterministically()
    {
        var service = CreateService(new InMemoryStateStore());
        var settings = new StarshipExpeditionSettings
        {
            LegCount = 8,
            DisabledBuiltInSectors = [.. StarshipExpeditionSettings.BuiltInSectors.Skip(1)],
            CustomSectors = ["Glass Nebula"]
        };
        var first = service.BuildPreview(settings, 881122);
        var second = service.BuildPreview(settings, 881122);

        Assert.Equal(first.Select(item => item.Sector), second.Select(item => item.Sector));
        Assert.All(first, item => Assert.Contains(item.Sector, new[] { "Kepler Reach", "Glass Nebula" }));
        Assert.Equal(2, first.Select(item => item.Sector).Distinct().Count());
    }

    [Fact]
    public async Task SectorSelection_ActiveVoyageKeepsDeparturePoolAcrossEditsAndRecovery()
    {
        var settings = new StarshipExpeditionSettings
        {
            LegCount = 6,
            DisabledBuiltInSectors = [.. StarshipExpeditionSettings.BuiltInSectors.Skip(2)]
        };
        var settingsStore = new InMemorySettingsStore(settings);
        var stateStore = new InMemoryStateStore();
        var service = CreateService(stateStore, settingsStore: settingsStore);
        var started = await service.StartExpeditionAsync("moderator", seed: 444);
        Assert.Equal(started.ExpeditionId, StarshipExpeditionOverlaySnapshot.From(started).ExpeditionId);
        await service.JoinCrewAsync(CreateArgs("pilot-id", "Pilot"));
        settings.DisabledBuiltInSectors = [.. StarshipExpeditionSettings.BuiltInSectors];
        settings.CustomSectors = ["New Sector One", "New Sector Two"];
        var restored = CreateService(stateStore, settingsStore: settingsStore);

        for (var transition = 0; transition < 6; transition++)
        {
            var current = await restored.AdvanceEventAsync();
            Assert.Equal(["Kepler Reach", "Ternion Belt"], current.SectorPool);
            Assert.Contains(current.CurrentSector, current.SectorPool);
        }
        Assert.Equal(["Kepler Reach", "Ternion Belt"], started.SectorPool);
        Assert.Equal(["New Sector One", "New Sector Two"], settings.GetEffectiveSectors());
    }

    [Fact]
    public async Task StartExpedition_SnapshotsSettingsAndSectorPoolForCurrentVoyage()
    {
        var settingsStore = new InMemorySettingsStore(new StarshipExpeditionSettings
        {
            LegCount = 6,
            StationIntervalLegs = 3,
            ActiveCrewRewardsOnly = false,
            RewardProfiles = [new StarshipRewardProfile { EventType = "SalvageSite", SuccessChancePercent = 62 }],
            CustomSectors = ["Cerulean Gate"]
        });
        var service = CreateService(new InMemoryStateStore(), settingsStore: settingsStore);

        var started = await service.StartExpeditionAsync("moderator", seed: 444);
        settingsStore.Settings.ShipSystems.StationRepairAmount = 1;
        settingsStore.Settings.CustomSectors.Add("Changed After Start");
        settingsStore.Settings.RewardProfiles[0].SuccessChancePercent = 10;
        var current = await service.GetSnapshotAsync();

        Assert.Equal(6, current!.LegCount);
        Assert.Equal(3, current.StationIntervalLegs);
        Assert.False(current.ActiveCrewRewardsOnly);
        Assert.Contains("Cerulean Gate", current.SectorPool);
        Assert.DoesNotContain("Changed After Start", current.SectorPool);
        Assert.Equal(62, Assert.Single(current.RewardProfiles).SuccessChancePercent);
        Assert.Equal(started.ExpeditionId, current.ExpeditionId);
        Assert.Equal(100, current.ShipSystems!.Hull);
        Assert.Equal(100, current.ShipSystems.Supplies);
        Assert.Equal(30, current.ShipSystems.Rules.StationRepairAmount);
    }

    [Fact]
    public async Task ResolveEvent_SnapshotsEligibilityAndAwardsDeterministicCappedCrewBalance()
    {
        var stateStore = new InMemoryStateStore();
        var settingsStore = new InMemorySettingsStore(new StarshipExpeditionSettings
        {
            RewardProfiles =
            [
                new StarshipRewardProfile
                {
                    EventType = "RouteChoice",
                    Enabled = true,
                    PointTypeId = 12,
                    SuccessChancePercent = 100,
                    CommonWeightPercent = 100,
                    UncommonWeightPercent = 0,
                    RareWeightPercent = 0,
                    CommonMinimum = 10,
                    CommonMaximum = 10,
                    MaximumPerEvent = 7,
                    MaximumUnbankedPerVoyage = 40
                }
            ]
        });
        var viewerFeature = new Mock<IViewerFeature>();
        viewerFeature.Setup(viewers => viewers.GetActiveViewers()).Returns(["pilot"]);
        viewerFeature.Setup(viewers => viewers.GetCurrentViewers()).Returns(["pilot", "engineer", "subscriber"]);
        viewerFeature.Setup(viewers => viewers.IsSubscriber("engineer")).ReturnsAsync(false);
        viewerFeature.Setup(viewers => viewers.IsSubscriber("subscriber")).ReturnsAsync(true);
        var points = new Mock<IPointsSystem>();
        points.Setup(system => system.GetPointTypeById(12)).ReturnsAsync(new PointType { Id = 12, Name = "Salvage Credits" });
        var triggerService = new Mock<IDefaultCommandTriggerService>();
        triggerService.Setup(trigger => trigger.TriggerDefaultCommandEventAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CommandEventArgs>(), It.IsAny<Dictionary<string, string>>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(stateStore, settingsStore: settingsStore, viewerFeatureMock: viewerFeature,
            defaultCommandTriggerService: triggerService.Object, pointsSystem: points.Object);

        await service.StartExpeditionAsync("moderator", legCount: 4, seed: 102);
        await service.JoinCrewAsync(CreateArgs("pilot-id", "Pilot"));
        await service.JoinCrewAsync(CreateArgs("engineer-id", "Engineer"));
        await service.JoinCrewAsync(CreateArgs("subscriber-id", "Subscriber"));
        await service.AdvanceEventAsync();
        await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "direct");
        await service.VoteAsync(CreateArgs("engineer-id", "Engineer"), "direct");
        var resolved = await service.AdvanceEventAsync();

        var resolvedEvent = Assert.Single(resolved.CompletedEvents);
        Assert.Equal(new[] { "pilot-id", "subscriber-id" }, resolvedEvent.EligibleRewardUserIds);
        Assert.Equal(new[] { "pilot-id", "subscriber-id" }, resolvedEvent.RewardOutcomes
            .Where(outcome => outcome.EarnedAmount > 0)
            .Select(outcome => outcome.UserId)
            .Order(StringComparer.OrdinalIgnoreCase));
        Assert.All(resolvedEvent.RewardOutcomes.Where(outcome => outcome.EarnedAmount > 0), outcome => Assert.Equal(7, outcome.EarnedAmount));
        Assert.Single(resolved.Crew.First(member => member.UserId == "pilot-id").UnbankedBalances);
        Assert.Empty(resolved.Crew.First(member => member.UserId == "engineer-id").UnbankedBalances);
        Assert.Single(resolved.Crew.First(member => member.UserId == "subscriber-id").UnbankedBalances);
        Assert.Equal(7, resolved.Crew.First(member => member.UserId == "pilot-id").UnbankedBalances[0].Amount);
        triggerService.Verify(trigger => trigger.TriggerDefaultCommandEventAsync(
            StarshipExpeditionService.DefaultCommandName,
            DefaultCommandEventTypes.StarshipEventResolved,
            It.IsAny<CommandEventArgs>(),
            It.Is<Dictionary<string, string>>(variables => variables["RewardTotal"] == "14" && variables["RewardParticipantCount"] == "2")), Times.Once);
    }

    [Fact]
    public async Task ResolveMultipleEvents_EnforcesPerVoyageUnbankedCap()
    {
        var eventTypes = new[] { "RouteChoice", "SignalDetected", "SalvageSite", "Hazard" };
        var rewardProfiles = eventTypes.Select(eventType => new StarshipRewardProfile
        {
            EventType = eventType,
            Enabled = true,
            PointTypeId = 4,
            SuccessChancePercent = 100,
            CommonWeightPercent = 100,
            UncommonWeightPercent = 0,
            RareWeightPercent = 0,
            CommonMinimum = 10,
            CommonMaximum = 10,
            MaximumPerEvent = 100,
            MaximumUnbankedPerVoyage = 10
        }).ToList();
        var settingsStore = new InMemorySettingsStore(new StarshipExpeditionSettings
        {
            ActiveCrewRewardsOnly = false,
            RewardProfiles = rewardProfiles
        });
        var points = new Mock<IPointsSystem>();
        points.Setup(system => system.GetPointTypeById(4)).ReturnsAsync(new PointType { Id = 4, Name = "Credits" });
        var service = CreateService(new InMemoryStateStore(), settingsStore: settingsStore, pointsSystem: points.Object);
        await service.StartExpeditionAsync("moderator", legCount: 5, seed: 106);
        await service.JoinCrewAsync(CreateArgs("pilot-id", "Pilot"));
        await service.AdvanceEventAsync();
        await service.AdvanceEventAsync();
        await service.AdvanceEventAsync();
        var afterSecondReward = await service.GetSnapshotAsync();

        Assert.Equal(10, Assert.Single(afterSecondReward!.Crew.Single().UnbankedBalances).Amount);
        Assert.Equal(10, afterSecondReward.CompletedEvents.Sum(item => item.RewardOutcomes.Sum(outcome => outcome.EarnedAmount)));
    }

    [Fact]
    public async Task RewardOutcome_ReplaysDeterministicallyFromSeedEventAndParticipant()
    {
        static StarshipExpeditionSnapshot CreateRewardScenario() => new()
        {
            ExpeditionId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Seed = 2048,
            StartedAtUtc = DateTimeOffset.UnixEpoch,
            UpdatedAtUtc = DateTimeOffset.UnixEpoch,
            Status = StarshipExpeditionStatus.EventOpen,
            CurrentLeg = 1,
            LegCount = 4,
            CurrentSector = "Kepler Reach",
            CurrentRoute = "Survey Route",
            SectorPool = ["Kepler Reach", "Ternion Belt"],
            ActiveCrewRewardsOnly = false,
            RewardProfiles =
            [
                new StarshipRewardProfile
                {
                    EventType = "SignalDetected", Enabled = true, PointTypeId = 9, PointTypeName = "Credits",
                    SuccessChancePercent = 55, CommonWeightPercent = 70, UncommonWeightPercent = 25, RareWeightPercent = 5,
                    CommonMinimum = 5, CommonMaximum = 15, UncommonMinimum = 20, UncommonMaximum = 35,
                    RareMinimum = 40, RareMaximum = 60, MaximumPerEvent = 100, MaximumUnbankedPerVoyage = 300
                }
            ],
            Crew = [new StarshipCrewMemberSnapshot { UserId = "pilot-id", UserName = "pilot", DisplayName = "Pilot" }],
            CurrentEvent = new StarshipExpeditionEventSnapshot
            {
                EventId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Sequence = 2,
                EventType = "SignalDetected",
                Title = "Unidentified Signal",
                Sector = "Kepler Reach",
                OpenedAtUtc = DateTimeOffset.UnixEpoch,
                ClosesAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(60),
                Choices = [new StarshipEventChoiceSnapshot { ChoiceId = "scan", Label = "Scan" }],
                DefaultChoiceId = "scan"
            }
        };

        var firstStore = new InMemoryStateStore();
        var secondStore = new InMemoryStateStore();
        var first = CreateService(firstStore);
        var second = CreateService(secondStore);
        await firstStore.SaveAsync(CreateRewardScenario());
        await secondStore.SaveAsync(CreateRewardScenario());

        var firstResult = await first.AdvanceEventAsync();
        var secondResult = await second.AdvanceEventAsync();

        var firstOutcome = Assert.Single(firstResult.CompletedEvents.Single().RewardOutcomes);
        var secondOutcome = Assert.Single(secondResult.CompletedEvents.Single().RewardOutcomes);
        Assert.Equal(firstOutcome.Outcome, secondOutcome.Outcome);
        Assert.Equal(firstOutcome.Rarity, secondOutcome.Rarity);
        Assert.Equal(firstOutcome.EarnedAmount, secondOutcome.EarnedAmount);
    }

    [Fact]
    public async Task VoyageCap_RemainsConsumedAfterStationBanking()
    {
        var expeditionId = Guid.NewGuid();
        var profile = new StarshipRewardProfile
        {
            EventType = "RouteChoice",
            Enabled = true,
            PointTypeId = 4,
            PointTypeName = "Credits",
            SuccessChancePercent = 100,
            CommonWeightPercent = 100,
            UncommonWeightPercent = 0,
            RareWeightPercent = 0,
            CommonMinimum = 10,
            CommonMaximum = 10,
            MaximumPerEvent = 50,
            MaximumUnbankedPerVoyage = 10
        };
        var stateStore = new InMemoryStateStore();
        await stateStore.SaveAsync(new StarshipExpeditionSnapshot
        {
            ExpeditionId = expeditionId,
            Seed = 78,
            StartedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Status = StarshipExpeditionStatus.EventOpen,
            CurrentLeg = 1,
            LegCount = 4,
            CurrentSector = "Kepler Reach",
            CurrentRoute = "Uncharted",
            SectorPool = ["Kepler Reach", "Ternion Belt"],
            ActiveCrewRewardsOnly = false,
            RewardProfiles = [profile],
            Crew = [new StarshipCrewMemberSnapshot { UserId = "pilot-id", DisplayName = "Pilot" }],
            CompletedEvents =
            [
                new StarshipExpeditionEventSnapshot
                {
                    EventId = Guid.NewGuid(),
                    Sequence = 1,
                    EventType = "RouteChoice",
                    RewardOutcomes = [new StarshipRewardOutcomeSnapshot
                    {
                        UserId = "pilot-id", DisplayName = "Pilot", PointTypeId = 4,
                        PointTypeName = "Credits", Outcome = "Success", EarnedAmount = 10
                    }]
                }
            ],
            BankedPayouts =
            [
                new StarshipBankedPayoutSnapshot
                {
                    OperationId = $"starship:{expeditionId:N}:old-bank:pilot-id:4",
                    EventId = Guid.NewGuid(),
                    UserId = "pilot-id",
                    PointTypeId = 4,
                    Amount = 10,
                    IsPaid = true
                }
            ],
            CurrentEvent = new StarshipExpeditionEventSnapshot
            {
                EventId = Guid.NewGuid(),
                Sequence = 2,
                EventType = "RouteChoice",
                Title = "Second Reward",
                Sector = "Kepler Reach",
                OpenedAtUtc = DateTimeOffset.UtcNow,
                ClosesAtUtc = DateTimeOffset.UtcNow.AddSeconds(60),
                Choices = [new StarshipEventChoiceSnapshot { ChoiceId = "direct", Label = "Direct" }],
                DefaultChoiceId = "direct"
            }
        });
        var service = CreateService(stateStore, settingsStore: new InMemorySettingsStore(), pointsSystem: Mock.Of<IPointsSystem>());

        var resolved = await service.AdvanceEventAsync();

        Assert.Equal(10, resolved.CompletedEvents.Sum(item => item.RewardOutcomes.Sum(outcome => outcome.EarnedAmount)));
        Assert.Equal(0, resolved.CompletedEvents.Last().RewardOutcomes.Single().EarnedAmount);
        Assert.Empty(resolved.Crew.Single().UnbankedBalances);
    }

    [Fact]
    public async Task RiskyChoice_DisclosesBalanceBeforeConfirmationAndLossAppliesOnlyToParticipant()
    {
        var stateStore = new InMemoryStateStore();
        var pointProfile = new StarshipRewardProfile
        {
            EventType = "SalvageSite",
            Enabled = true,
            PointTypeId = 2,
            PointTypeName = "Salvage Credits",
            SuccessChancePercent = 0,
            CommonWeightPercent = 100,
            UncommonWeightPercent = 0,
            RareWeightPercent = 0,
            RiskLossPercent = 25,
            MaximumRiskLossPerEvent = 50
        };
        await stateStore.SaveAsync(new StarshipExpeditionSnapshot
        {
            ExpeditionId = Guid.NewGuid(),
            Seed = 33,
            StartedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Status = StarshipExpeditionStatus.EventOpen,
            CurrentLeg = 1,
            LegCount = 4,
            CurrentSector = "Ternion Belt",
            CurrentRoute = "Survey Route",
            SectorPool = ["Ternion Belt", "Kepler Reach"],
            ActiveCrewRewardsOnly = false,
            RewardProfiles = [pointProfile],
            Crew =
            [
                new StarshipCrewMemberSnapshot
                {
                    UserId = "pilot-id", UserName = "pilot", DisplayName = "Pilot",
                    UnbankedBalances = [new StarshipUnbankedBalanceSnapshot { PointTypeId = 2, PointTypeName = "Salvage Credits", Amount = 80 }]
                },
                new StarshipCrewMemberSnapshot
                {
                    UserId = "engineer-id", UserName = "engineer", DisplayName = "Engineer",
                    UnbankedBalances = [new StarshipUnbankedBalanceSnapshot { PointTypeId = 2, PointTypeName = "Salvage Credits", Amount = 30 }]
                }
            ],
            CurrentEvent = new StarshipExpeditionEventSnapshot
            {
                EventId = Guid.NewGuid(),
                Sequence = 2,
                EventType = "SalvageSite",
                Title = "Derelict Salvage",
                Sector = "Ternion Belt",
                OpenedAtUtc = DateTimeOffset.UtcNow,
                ClosesAtUtc = DateTimeOffset.UtcNow.AddSeconds(60),
                DefaultChoiceId = "pass",
                Choices =
                [
                    new StarshipEventChoiceSnapshot { ChoiceId = "salvage", Label = "Recover salvage", IsRisky = true },
                    new StarshipEventChoiceSnapshot { ChoiceId = "pass", Label = "Pass it by" }
                ]
            }
        });
        var triggerService = new Mock<IDefaultCommandTriggerService>();
        triggerService.Setup(trigger => trigger.TriggerDefaultCommandEventAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CommandEventArgs>(), It.IsAny<Dictionary<string, string>>()))
            .Returns(Task.CompletedTask);
        var replies = new List<string>();
        var serviceBackbone = new Mock<IServiceBackbone>();
        serviceBackbone.Setup(backbone => backbone.SendChatMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, string, bool>((_, message, _) => replies.Add(message))
            .Returns(Task.CompletedTask);
        var service = CreateService(stateStore, settingsStore: new InMemorySettingsStore(),
            serviceBackboneMock: serviceBackbone, defaultCommandTriggerService: triggerService.Object,
            pointsSystem: Mock.Of<IPointsSystem>());

        var riskyVote = CreateArgs("pilot-id", "Pilot");
        riskyVote.Args = ["vote", "1"];
        await service.OnCommand(null, riskyVote);
        Assert.Contains(replies, reply => reply.Contains("unbanked Salvage Credits balance is 80", StringComparison.Ordinal)
            && reply.Contains("lose up to 20", StringComparison.Ordinal)
            && reply.Contains("Success chance 0%", StringComparison.Ordinal)
            && reply.Contains("Rarity: unknown", StringComparison.Ordinal)
            && reply.Contains("!sc", StringComparison.Ordinal));
        Assert.DoesNotContain(replies, reply => reply.Contains("Rare 30-50", StringComparison.Ordinal));
        var pending = await service.GetSnapshotAsync();
        Assert.NotNull(pending);
        var confirmation = Assert.Single(pending.CurrentEvent!.PendingRiskConfirmations);
        Assert.Equal(80, confirmation.ExposedBalance);
        Assert.Equal(20, confirmation.MaximumLoss);
        Assert.Empty(pending.CurrentEvent.Votes);

        var shortConfirm = CreateArgs("pilot-id", "Pilot");
        shortConfirm.Command = StarshipExpeditionService.ShortConfirmCommandName;
        shortConfirm.Args = [];
        await service.OnCommand(null, shortConfirm);
        Assert.Contains(replies, reply => reply.Contains("Confirmed and committed: Recover salvage", StringComparison.Ordinal));
        var resolved = await service.AdvanceEventAsync();

        var pilot = resolved.Crew.Single(member => member.UserId == "pilot-id");
        var engineer = resolved.Crew.Single(member => member.UserId == "engineer-id");
        Assert.Equal(60, Assert.Single(pilot.UnbankedBalances).Amount);
        Assert.Equal(30, Assert.Single(engineer.UnbankedBalances).Amount);
        Assert.Equal("salvage", resolved.CompletedEvents.Single().ResolvedChoiceId);
        Assert.Equal("Failure", resolved.CompletedEvents.Single().OutcomeResult);
        var riskOutcome = Assert.Single(resolved.CompletedEvents.Single().RewardOutcomes);
        Assert.Equal("pilot-id", riskOutcome.UserId);
        Assert.Equal("Failure", riskOutcome.Outcome);
        Assert.Equal(20, riskOutcome.LostAmount);
        var failureMessage = StarshipExpeditionNarrative.BuildEventMessage(resolved.CompletedEvents.Single());
        Assert.Contains("FAILED", failureMessage, StringComparison.Ordinal);
        Assert.Contains("lost 20 Salvage Credits", failureMessage, StringComparison.Ordinal);
        triggerService.Verify(trigger => trigger.TriggerDefaultCommandEventAsync(
            StarshipExpeditionService.DefaultCommandName,
            DefaultCommandEventTypes.StarshipRiskFailed,
            It.Is<CommandEventArgs>(args => args.UserId == "pilot-id"),
            It.Is<Dictionary<string, string>>(variables => variables["LostAmount"] == "20")), Times.Once);
    }

    private static StarshipExpeditionSnapshot CreateSalvageFixture(int pilotBalance = 100, StarshipShipSystemsSnapshot? shipSystems = null) => new()
    {
        ExpeditionId = Guid.NewGuid(),
        Seed = 71,
        ShipSystems = shipSystems,
        StartedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
        Status = StarshipExpeditionStatus.EventOpen,
        CurrentLeg = 1,
        LegCount = 4,
        CurrentSector = "Ternion Belt",
        CurrentRoute = "Survey Route",
        SectorPool = ["Ternion Belt", "Kepler Reach"],
        EventVoteDurationSeconds = 60,
        ActiveCrewRewardsOnly = false,
        RewardProfiles =
        [
            new StarshipRewardProfile
            {
                EventType = "SalvageSite", Enabled = true, PointTypeId = 2, PointTypeName = "Salvage Credits",
                SuccessChancePercent = 80, CommonWeightPercent = 100, UncommonWeightPercent = 0, RareWeightPercent = 0,
                CommonMinimum = 10, CommonMaximum = 10, MaximumPerEvent = 100, MaximumUnbankedPerVoyage = 500,
                RiskLossPercent = 40, MaximumRiskLossPerEvent = 100
            }
        ],
        Crew =
        [
            new StarshipCrewMemberSnapshot
            {
                UserId = "pilot-id", UserName = "pilot", DisplayName = "Pilot",
                UnbankedBalances = [new StarshipUnbankedBalanceSnapshot { PointTypeId = 2, PointTypeName = "Salvage Credits", Amount = pilotBalance }]
            }
        ],
        CurrentEvent = CreateSalvageEvent()
    };

    private static StarshipExpeditionEventSnapshot CreateSalvageEvent()
    {
        StarshipEventChoiceSnapshot[] pool =
        [
            new() { ChoiceId = "scan", Label = "Scan the wreck", ContinuesEvent = true, GrantsFlag = "scanned", RevealsRarity = true, Story = "{crew} sweeps the sensors over {subject}." },
            new() { ChoiceId = "secure", Label = "Secure the area", ContinuesEvent = true, GrantsFlag = "secured", SuccessChanceModifierPercent = 20, RewardMultiplierPercent = 80, RiskMultiplierPercent = 50 },
            new() { ChoiceId = "salvage", Label = "Recover salvage", IsRisky = true, Story = "{crew} boards {subject}." },
            new() { ChoiceId = "pass", Label = "Pass it by" }
        ];
        return new StarshipExpeditionEventSnapshot
        {
            EventId = Guid.NewGuid(),
            Sequence = 2,
            EventType = "SalvageSite",
            Title = "Derelict Salvage",
            Sector = "Ternion Belt",
            OpenedAtUtc = DateTimeOffset.UtcNow,
            ClosesAtUtc = DateTimeOffset.UtcNow.AddSeconds(60),
            DefaultChoiceId = "pass",
            Subject = "the ISV Quiet Meridian",
            SuccessStory = "The crew hauls out {loot}.",
            Choices = [.. pool],
            ChoicePool = [.. pool]
        };
    }

    [Fact]
    public async Task SalvageEvent_ScanThenSecureThenSalvage_ReportsRevealedRarityModifiersAndOutcome()
    {
        var stateStore = new InMemoryStateStore();
        await stateStore.SaveAsync(CreateSalvageFixture());
        var chatMessages = new List<string>();
        var serviceBackbone = new Mock<IServiceBackbone>();
        serviceBackbone.Setup(backbone => backbone.SendChatMessage(It.IsAny<string>()))
            .Callback<string>(chatMessages.Add)
            .Returns(Task.CompletedTask);
        serviceBackbone.Setup(backbone => backbone.SendChatMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(stateStore, settingsStore: new InMemorySettingsStore(),
            serviceBackboneMock: serviceBackbone, pointsSystem: Mock.Of<IPointsSystem>());

        var aliasVote = CreateArgs("pilot-id", "Pilot");
        aliasVote.Command = StarshipExpeditionService.ShortVoteCommandName;
        aliasVote.Args = ["1"];
        await service.OnCommand(null, aliasVote);
        var afterScan = await service.AdvanceEventAsync();

        Assert.Empty(afterScan.CompletedEvents);
        Assert.Equal(2, afterScan.CurrentEvent!.StepNumber);
        Assert.Equal("Common", afterScan.CurrentEvent.RevealedRarity);
        Assert.DoesNotContain(afterScan.CurrentEvent.Choices, choice => choice.ChoiceId == "scan");
        Assert.Contains(chatMessages, message => message.Contains("Known rarity: Common", StringComparison.Ordinal));

        await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "secure");
        var afterSecure = await service.AdvanceEventAsync();

        Assert.Equal(3, afterSecure.CurrentEvent!.StepNumber);
        Assert.Equal(20, afterSecure.CurrentEvent.SuccessChanceModifierPercent);
        Assert.Equal(80, afterSecure.CurrentEvent.RewardMultiplierPercent);
        Assert.Equal(50, afterSecure.CurrentEvent.RiskMultiplierPercent);
        Assert.Equal(["salvage", "pass"], afterSecure.CurrentEvent.Choices.Select(choice => choice.ChoiceId));

        var prompt = await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "1");
        var confirmation = Assert.Single(prompt.CurrentEvent!.PendingRiskConfirmations);
        Assert.Equal(20, confirmation.MaximumLoss);
        Assert.Equal(100, confirmation.SuccessChancePercent);
        Assert.Equal(80, confirmation.RewardMultiplierPercent);
        Assert.Equal("Common", confirmation.RevealedRarity);
        await service.ConfirmRiskActionAsync(CreateArgs("pilot-id", "Pilot"));

        var resolved = await service.AdvanceEventAsync();

        var completed = Assert.Single(resolved.CompletedEvents);
        Assert.Equal("Success", completed.OutcomeResult);
        Assert.Equal("Common", completed.OutcomeRarity);
        Assert.Equal(["Scan the wreck", "Secure the area", "Recover salvage"], completed.Path.Select(step => step.Label));
        Assert.Equal(108, Assert.Single(resolved.Crew.Single().UnbankedBalances).Amount);
        Assert.Equal(8, Assert.Single(completed.RewardOutcomes).EarnedAmount);
        Assert.Contains(chatMessages, message => message.Contains("SUCCESS - Common find", StringComparison.Ordinal)
            && message.Contains("Pilot boards the ISV Quiet Meridian. The crew hauls out salvage plating and spare parts.", StringComparison.Ordinal));
        Assert.Contains(chatMessages, message => message.Contains("Pilot sweeps the sensors over the ISV Quiet Meridian.", StringComparison.Ordinal));
        Assert.Contains(chatMessages, message => message.Contains("SUCCESS - Common find", StringComparison.Ordinal)
            && message.Contains("Earned 8 Salvage Credits", StringComparison.Ordinal)
            && message.Contains("Scan the wreck > Secure the area > Recover salvage", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SalvageEvent_GoingStraightToSalvage_HasHigherRiskAndFullRewards()
    {
        var stateStore = new InMemoryStateStore();
        await stateStore.SaveAsync(CreateSalvageFixture());
        var service = CreateService(stateStore, settingsStore: new InMemorySettingsStore(), pointsSystem: Mock.Of<IPointsSystem>());

        var prompt = await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "salvage");

        var confirmation = Assert.Single(prompt.CurrentEvent!.PendingRiskConfirmations);
        Assert.Equal(40, confirmation.MaximumLoss);
        Assert.Equal(80, confirmation.SuccessChancePercent);
        Assert.Equal(100, confirmation.RewardMultiplierPercent);
        Assert.Null(confirmation.RevealedRarity);
    }

    [Fact]
    public async Task SalvageEvent_PassingWithoutCommitting_EndsWithoutRewardOrLoss()
    {
        var stateStore = new InMemoryStateStore();
        await stateStore.SaveAsync(CreateSalvageFixture());
        var service = CreateService(stateStore, settingsStore: new InMemorySettingsStore(), pointsSystem: Mock.Of<IPointsSystem>());

        await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "pass");
        var resolved = await service.AdvanceEventAsync();

        var completed = Assert.Single(resolved.CompletedEvents);
        Assert.Equal("Passed", completed.OutcomeResult);
        Assert.Empty(completed.RewardOutcomes);
        Assert.Equal(100, Assert.Single(resolved.Crew.Single().UnbankedBalances).Amount);
    }

    [Fact]
    public async Task EventWithoutRiskyChoices_PaysSharedRewardUnlessCrewForfeits()
    {
        StarshipEventChoiceSnapshot[] pool =
        [
            new() { ChoiceId = "salvage", Label = "Recover salvage" },
            new() { ChoiceId = "pass", Label = "Pass it by", EndsWithoutReward = true }
        ];
        var results = new List<StarshipExpeditionSnapshot>();
        foreach (var vote in new[] { "salvage", "pass" })
        {
            var fixture = CreateSalvageFixture();
            fixture.RewardProfiles[0].SuccessChancePercent = 100;
            fixture.CurrentEvent!.Choices = [.. pool];
            fixture.CurrentEvent.ChoicePool = [.. pool];
            var stateStore = new InMemoryStateStore();
            await stateStore.SaveAsync(fixture);
            var service = CreateService(stateStore, settingsStore: new InMemorySettingsStore(), pointsSystem: Mock.Of<IPointsSystem>());
            await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), vote);
            results.Add(await service.AdvanceEventAsync());
        }

        Assert.Equal("Success", results[0].CompletedEvents.Single().OutcomeResult);
        Assert.Equal(110, results[0].Crew.Single().UnbankedBalances.Single().Amount);
        Assert.Equal("Passed", results[1].CompletedEvents.Single().OutcomeResult);
        Assert.Equal(100, results[1].Crew.Single().UnbankedBalances.Single().Amount);
    }

    [Fact]
    public async Task FailedSharedEvent_CostsWholeCrewUnbankedPointsScaledByRiskModifiers()
    {
        StarshipEventChoiceSnapshot[] pool =
        [
            new() { ChoiceId = "secure", Label = "Secure the area", ContinuesEvent = true, GrantsFlag = "secured", RiskMultiplierPercent = 50 },
            new() { ChoiceId = "salvage", Label = "Recover salvage" }
        ];
        var losses = new List<long>();
        foreach (var secureFirst in new[] { false, true })
        {
            var fixture = CreateSalvageFixture();
            fixture.RewardProfiles[0].SuccessChancePercent = 0;
            fixture.Crew.Add(new StarshipCrewMemberSnapshot
            {
                UserId = "engineer-id", UserName = "engineer", DisplayName = "Engineer",
                UnbankedBalances = [new StarshipUnbankedBalanceSnapshot { PointTypeId = 2, PointTypeName = "Salvage Credits", Amount = 50 }]
            });
            fixture.CurrentEvent!.Choices = [.. pool];
            fixture.CurrentEvent.ChoicePool = [.. pool];
            var stateStore = new InMemoryStateStore();
            await stateStore.SaveAsync(fixture);
            var service = CreateService(stateStore, settingsStore: new InMemorySettingsStore(), pointsSystem: Mock.Of<IPointsSystem>());
            if (secureFirst)
            {
                await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "secure");
                await service.AdvanceEventAsync();
            }

            await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "salvage");
            var resolved = await service.AdvanceEventAsync();
            Assert.Equal("Failure", resolved.CompletedEvents.Single().OutcomeResult);
            losses.Add(resolved.CompletedEvents.Single().RewardOutcomes.Sum(outcome => outcome.LostAmount));
            Assert.Equal(2, resolved.CompletedEvents.Single().RewardOutcomes.Count(outcome => outcome.LostAmount > 0));
        }

        Assert.Equal(60, losses[0]);
        Assert.Equal(30, losses[1]);
    }

    [Fact]
    public async Task ShowStatusWidget_SetsForcedVisibilityWindowAndBroadcastsIt()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var stateStore = new InMemoryStateStore();
        var service = CreateService(stateStore, clock);
        Assert.Null(await service.ShowStatusWidgetAsync(30));

        await service.StartExpeditionAsync("moderator", seed: 5);
        var shown = await service.ShowStatusWidgetAsync(45);

        Assert.Equal(clock.GetUtcNow().AddSeconds(45), shown!.StatusForcedUntilUtc);
        Assert.Equal(shown.StatusForcedUntilUtc, StarshipExpeditionOverlaySnapshot.From(shown).StatusForcedUntilUtc);
        Assert.Equal(shown.StatusForcedUntilUtc, (await stateStore.LoadAsync())!.StatusForcedUntilUtc);
        var capped = await service.ShowStatusWidgetAsync(100000);
        Assert.Equal(clock.GetUtcNow().AddSeconds(3600), capped!.StatusForcedUntilUtc);
    }

    [Fact]
    public async Task MultiStepEvent_FiresStepAdvancedAndOutcomeTriggers()
    {
        var stateStore = new InMemoryStateStore();
        await stateStore.SaveAsync(CreateSalvageFixture());
        var firedEvents = new List<string>();
        var triggerService = new Mock<IDefaultCommandTriggerService>();
        triggerService.Setup(trigger => trigger.TriggerDefaultCommandEventAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CommandEventArgs>(), It.IsAny<Dictionary<string, string>>()))
            .Callback<string, string, CommandEventArgs, Dictionary<string, string>>((_, eventType, _, _) => firedEvents.Add(eventType))
            .Returns(Task.CompletedTask);
        var service = CreateService(stateStore, settingsStore: new InMemorySettingsStore(),
            defaultCommandTriggerService: triggerService.Object, pointsSystem: Mock.Of<IPointsSystem>());

        await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "scan");
        await service.AdvanceEventAsync();
        Assert.Equal(1, firedEvents.Count(item => item == DefaultCommandEventTypes.StarshipEventStepAdvanced));
        Assert.DoesNotContain(DefaultCommandEventTypes.StarshipEventResolved, firedEvents);

        await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "pass");
        await service.AdvanceEventAsync();
        Assert.Contains(DefaultCommandEventTypes.StarshipEventResolved, firedEvents);
        Assert.DoesNotContain(DefaultCommandEventTypes.StarshipEventSucceeded, firedEvents);
        Assert.DoesNotContain(DefaultCommandEventTypes.StarshipEventFailed, firedEvents);
    }

    [Theory]
    [InlineData(100, "Starship.EventSucceeded")]
    [InlineData(0, "Starship.EventFailed")]
    public async Task CompletedRewardEvent_FiresMatchingOutcomeTrigger(int successChance, string expectedTrigger)
    {
        var fixture = CreateSalvageFixture();
        fixture.RewardProfiles[0].SuccessChancePercent = successChance;
        var stateStore = new InMemoryStateStore();
        await stateStore.SaveAsync(fixture);
        var firedEvents = new List<string>();
        var triggerService = new Mock<IDefaultCommandTriggerService>();
        triggerService.Setup(trigger => trigger.TriggerDefaultCommandEventAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CommandEventArgs>(), It.IsAny<Dictionary<string, string>>()))
            .Callback<string, string, CommandEventArgs, Dictionary<string, string>>((_, eventType, _, _) => firedEvents.Add(eventType))
            .Returns(Task.CompletedTask);
        var service = CreateService(stateStore, settingsStore: new InMemorySettingsStore(),
            defaultCommandTriggerService: triggerService.Object, pointsSystem: Mock.Of<IPointsSystem>());

        await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "salvage");
        await service.ConfirmRiskActionAsync(CreateArgs("pilot-id", "Pilot"));
        await service.AdvanceEventAsync();

        Assert.Equal(1, firedEvents.Count(item => item == expectedTrigger));
        Assert.Equal(1, firedEvents.Count(item => item is "Starship.EventSucceeded" or "Starship.EventFailed"));
    }

    [Fact]
    public async Task Ship_IsGeneratedFromTheSeedAndExposedWithStationLegs()
    {
        var service = CreateService(new InMemoryStateStore(), settingsStore: new InMemorySettingsStore(
            new StarshipExpeditionSettings { LegCount = 6, StationIntervalLegs = 2 }));
        var started = await service.StartExpeditionAsync("moderator", seed: 777);

        Assert.NotEmpty(started.Ship.Name);
        Assert.Contains(started.Ship.HullStyle, new[] { "arrow", "hauler", "scout", "cruiser" });
        Assert.Matches("^#[0-9a-f]{6}$", started.Ship.AccentColor);
        Assert.Equal(started.Ship.Name, StarshipExpeditionStory.GenerateShip(777).Name);
        Assert.Contains(Enumerable.Range(1, 60), seed => StarshipExpeditionStory.GenerateShip(seed).HullStyle != started.Ship.HullStyle);

        var projection = StarshipExpeditionOverlaySnapshot.From(started);
        Assert.Equal(started.Ship.Name, projection.Ship.Name);
        Assert.Equal([3, 5], projection.StationLegs);
        Assert.Empty(StarshipExpeditionOverlaySnapshot.From(new StarshipExpeditionSnapshot { LegCount = 5, StationIntervalLegs = 0 }).StationLegs);
    }

    [Fact]
    public async Task StationLegs_MatchTheLegsWhereStationStopsActuallyOccur()
    {
        var service = CreateService(new InMemoryStateStore());
        var preview = service.BuildPreview(new StarshipExpeditionSettings { LegCount = 9, StationIntervalLegs = 3 }, seed: 5);
        var projectedStations = StarshipExpeditionOverlaySnapshot.GetStationLegs(
            new StarshipExpeditionSnapshot { LegCount = 9, StationIntervalLegs = 3 });

        Assert.Equal(
            preview.Where(item => item.EventType == "StationStop").Select(item => item.Sequence),
            projectedStations);
        await Task.CompletedTask;
    }

    [Fact]
    public void BuildPreview_GivesEveryEventADeterministicObjectLook()
    {
        var service = CreateService(new InMemoryStateStore());
        var settings = new StarshipExpeditionSettings { LegCount = 24 };
        var first = service.BuildPreview(settings, seed: 303);
        var second = service.BuildPreview(settings, seed: 303);

        Assert.Equal(first.Select(item => (item.ObjectKind, item.ObjectVariant)), second.Select(item => (item.ObjectKind, item.ObjectVariant)));
        Assert.All(first, item =>
        {
            Assert.Equal(StarshipExpeditionStory.GetObjectKind(item.EventType), item.ObjectKind);
            Assert.InRange(item.ObjectVariant, 0, StarshipExpeditionStory.ObjectVariantCount - 1);
        });
        Assert.Equal("fork", first[0].ObjectKind);
        Assert.True(first.Select(item => item.ObjectVariant).Distinct().Count() > 1);
    }

    [Fact]
    public async Task OverlayProjection_ExposesStructuredEffectsTimelineAndStoryWithoutDuplicatingIt()
    {
        var fixture = CreateSalvageFixture();
        fixture.RewardProfiles[0].SuccessChancePercent = 100;
        var stateStore = new InMemoryStateStore();
        await stateStore.SaveAsync(fixture);
        var service = CreateService(stateStore, settingsStore: new InMemorySettingsStore(), pointsSystem: Mock.Of<IPointsSystem>());

        var open = StarshipExpeditionOverlaySnapshot.From((await service.GetSnapshotAsync())!);
        var secure = open.CurrentEvent!.Choices.Single(choice => choice.ChoiceId == "secure");
        Assert.Equal(20, secure.SuccessModifierPercent);
        Assert.Equal(80, secure.RewardMultiplierPercent);
        Assert.Equal(50, secure.RiskMultiplierPercent);
        Assert.True(secure.ContinuesEvent);
        Assert.True(open.CurrentEvent.Choices.Single(choice => choice.ChoiceId == "scan").RevealsRarity);
        Assert.Equal("the ISV Quiet Meridian", open.CurrentEvent.Subject);

        await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "pass");
        var resolved = await service.AdvanceEventAsync();
        var projection = StarshipExpeditionOverlaySnapshot.From(resolved);

        var entry = Assert.Single(projection.Timeline);
        Assert.Equal("Derelict Salvage", entry.Title);
        Assert.Equal("Passed", entry.Outcome);
        Assert.NotNull(projection.LatestResult);
        Assert.DoesNotContain(projection.LatestResult!.Lines, line => line == projection.LatestResult.Story && line.Length > 0);
    }

    [Fact]
    public async Task Appearance_IsSentWithStateAndRefreshPushesSavedChangesEvenWithoutAVoyage()
    {
        var broadcasts = new List<string>();
        var settingsStore = new InMemorySettingsStore();
        var service = CreateService(new InMemoryStateStore(), settingsStore: settingsStore, broadcasts: broadcasts);

        Assert.Equal("#66d0c2", (await service.GetAppearanceAsync()).AccentColor);
        settingsStore.Settings.Appearance.AccentColor = "#8fb8ff";
        settingsStore.Settings.Appearance.ShipImages = ["/media/ship.png"];
        await service.RefreshAppearanceAsync();

        Assert.Equal("#8fb8ff", (await service.GetAppearanceAsync()).AccentColor);
        using (var idle = JsonDocument.Parse(Assert.Single(broadcasts)))
        {
            Assert.Equal(JsonValueKind.Null, idle.RootElement.GetProperty("snapshot").ValueKind);
            Assert.Equal("#8fb8ff", idle.RootElement.GetProperty("appearance").GetProperty("accentColor").GetString());
            Assert.Equal("/media/ship.png", idle.RootElement.GetProperty("appearance").GetProperty("shipImages")[0].GetString());
        }

        await service.StartExpeditionAsync("moderator", seed: 11);
        using var started = JsonDocument.Parse(broadcasts[^1]);
        Assert.Equal("Boarding", started.RootElement.GetProperty("snapshot").GetProperty("status").GetString());
        Assert.Equal("#8fb8ff", started.RootElement.GetProperty("appearance").GetProperty("accentColor").GetString());
        Assert.True(started.RootElement.GetProperty("snapshot").GetProperty("ship").GetProperty("variant").GetInt32() >= 0);
    }

    [Fact]
    public async Task EventObjectRoll_IsDeterministicPerSeedAndSequence()
    {
        var service = CreateService(new InMemoryStateStore());
        var settings = new StarshipExpeditionSettings { LegCount = 12 };
        var first = service.BuildPreview(settings, seed: 909);
        var second = service.BuildPreview(settings, seed: 909);

        Assert.Equal(first.Select(item => item.ObjectRoll), second.Select(item => item.ObjectRoll));
        Assert.True(first.Select(item => item.ObjectRoll).Distinct().Count() > 1);
        Assert.Equal(StarshipExpeditionStory.GenerateShip(5).Variant, StarshipExpeditionStory.GenerateShip(5).Variant);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ActivityFeed_RecordsLifecycleWithIncreasingIdsAndServerTime()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService(new InMemoryStateStore(), clock);
        await service.StartExpeditionAsync("moderator", seed: 61);

        await service.JoinCrewAsync(CreateArgs("viewer-1", "Pilot"));
        await service.AdvanceEventAsync();
        var voted = await service.VoteAsync(CreateArgs("viewer-2", "Newcomer"), "1");

        var kinds = voted.ActivityFeed.Select(item => item.Kind).ToList();
        Assert.Equal([StarshipActivityKinds.CrewJoined, StarshipActivityKinds.EventOpened, StarshipActivityKinds.CrewJoined], kinds);
        Assert.Equal([1L, 2L, 3L], voted.ActivityFeed.Select(item => item.Id));
        Assert.Equal("Pilot joined the crew - 1 aboard", voted.ActivityFeed[0].Text);
        Assert.Equal("Newcomer", voted.ActivityFeed[2].Actor);
        Assert.All(voted.ActivityFeed, item => Assert.Equal(clock.GetUtcNow(), item.AtUtc));

        var projection = StarshipExpeditionOverlaySnapshot.From(voted, clock.GetUtcNow());
        Assert.Equal(clock.GetUtcNow(), projection.ServerNowUtc);
        Assert.Equal(3, projection.Activity.Count);
    }

    [Fact]
    public async Task ActivityFeed_IsCappedAndEndsWithVoyageSummary()
    {
        var service = CreateService(new InMemoryStateStore());
        await service.StartExpeditionAsync("moderator", legCount: 1, seed: 62);
        for (var index = 0; index < 30; index++)
            await service.JoinCrewAsync(CreateArgs($"viewer-{index}", $"Viewer{index}"));
        await service.AdvanceEventAsync();
        await service.AdvanceEventAsync();

        var ended = (await service.GetSnapshotAsync())!;
        Assert.Equal(StarshipExpeditionStatus.Returned, ended.Status);
        Assert.Equal(StarshipExpeditionOverlaySnapshot.MaximumActivityItems, ended.ActivityFeed.Count);
        Assert.Equal(StarshipActivityKinds.VoyageEnded, ended.ActivityFeed[^1].Kind);
        Assert.Equal(StarshipActivityKinds.EventResolved, ended.ActivityFeed[^2].Kind);
        Assert.Equal(ended.ActivityFeed.Select(item => item.Id).Order(), ended.ActivityFeed.Select(item => item.Id));
    }

    [Fact]
    public async Task MultiStepEvent_AddsStepAndRarityActivity()
    {
        var stateStore = new InMemoryStateStore();
        await stateStore.SaveAsync(CreateSalvageFixture());
        var service = CreateService(stateStore, settingsStore: new InMemorySettingsStore(), pointsSystem: Mock.Of<IPointsSystem>());

        await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "scan");
        var afterScan = await service.AdvanceEventAsync();

        Assert.Contains(afterScan.ActivityFeed, item => item.Kind == StarshipActivityKinds.StepAdvanced && item.Actor == "Pilot");
        Assert.Contains(afterScan.ActivityFeed, item => item.Kind == StarshipActivityKinds.RarityRevealed && item.Text == "Common find revealed");
    }

    [Fact]
    public async Task RiskyChoice_DoesNotRewardOrPenalizeCrewThatBecomesIneligibleBeforeResolution()
    {
        var stateStore = new InMemoryStateStore();
        await stateStore.SaveAsync(new StarshipExpeditionSnapshot
        {
            ExpeditionId = Guid.NewGuid(), Seed = 32,
            StartedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow,
            Status = StarshipExpeditionStatus.EventOpen, CurrentLeg = 1, LegCount = 4,
            CurrentSector = "Ternion Belt", CurrentRoute = "Survey Route",
            SectorPool = ["Ternion Belt", "Kepler Reach"], ActiveCrewRewardsOnly = true,
            RewardProfiles =
            [
                new StarshipRewardProfile
                {
                    EventType = "SalvageSite", Enabled = true, PointTypeId = 2, PointTypeName = "Salvage Credits",
                    SuccessChancePercent = 0, CommonWeightPercent = 100, UncommonWeightPercent = 0, RareWeightPercent = 0,
                    RiskLossPercent = 50, MaximumRiskLossPerEvent = 50
                }
            ],
            Crew =
            [
                new StarshipCrewMemberSnapshot
                {
                    UserId = "pilot-id", UserName = "pilot", DisplayName = "Pilot",
                    UnbankedBalances = [new StarshipUnbankedBalanceSnapshot { PointTypeId = 2, PointTypeName = "Salvage Credits", Amount = 80 }]
                }
            ],
            CurrentEvent = new StarshipExpeditionEventSnapshot
            {
                EventId = Guid.NewGuid(), Sequence = 2, EventType = "SalvageSite", Title = "Derelict Salvage",
                Sector = "Ternion Belt", OpenedAtUtc = DateTimeOffset.UtcNow,
                ClosesAtUtc = DateTimeOffset.UtcNow.AddSeconds(60), DefaultChoiceId = "pass",
                Choices =
                [
                    new StarshipEventChoiceSnapshot { ChoiceId = "salvage", Label = "Recover salvage", IsRisky = true },
                    new StarshipEventChoiceSnapshot { ChoiceId = "pass", Label = "Pass it by" }
                ]
            }
        });
        var activeNames = new List<string> { "pilot" };
        var viewerFeature = new Mock<IViewerFeature>();
        viewerFeature.Setup(viewers => viewers.GetActiveViewers()).Returns(() => activeNames.ToList());
        viewerFeature.Setup(viewers => viewers.GetCurrentViewers()).Returns([]);
        var service = CreateService(stateStore, settingsStore: new InMemorySettingsStore(),
            viewerFeatureMock: viewerFeature, pointsSystem: Mock.Of<IPointsSystem>());

        await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "salvage");
        await service.ConfirmRiskActionAsync(CreateArgs("pilot-id", "Pilot"));
        activeNames.Clear();
        var resolved = await service.AdvanceEventAsync();

        Assert.Equal(80, Assert.Single(resolved.Crew.Single().UnbankedBalances).Amount);
        var outcome = Assert.Single(resolved.CompletedEvents.Single().RewardOutcomes);
        Assert.Equal("Ineligible", outcome.Outcome);
        Assert.Equal(0, outcome.EarnedAmount);
        Assert.Equal(0, outcome.LostAmount);
    }

    [Fact]
    public async Task StationPayout_RetriesAfterSnapshotSaveFailureWithoutDoubleCrediting()
    {
        var stateStore = new InMemoryStateStore();
        await stateStore.SaveAsync(new StarshipExpeditionSnapshot
        {
            ExpeditionId = Guid.NewGuid(),
            Seed = 77,
            StartedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Status = StarshipExpeditionStatus.EventOpen,
            CurrentLeg = 2,
            LegCount = 5,
            CurrentSector = "Vela Expanse",
            CurrentRoute = "Direct Route",
            ActiveCrewRewardsOnly = false,
            Crew =
            [
                new StarshipCrewMemberSnapshot
                {
                    UserId = "pilot-id", UserName = "pilot", DisplayName = "Pilot",
                    UnbankedBalances = [new StarshipUnbankedBalanceSnapshot { PointTypeId = 3, PointTypeName = "Credits", Amount = 25 }]
                }
            ],
            CurrentEvent = new StarshipExpeditionEventSnapshot
            {
                EventId = Guid.NewGuid(),
                Sequence = 4,
                EventType = "StationStop",
                Title = "Space Station",
                Sector = "Vela Expanse",
                OpenedAtUtc = DateTimeOffset.UtcNow,
                ClosesAtUtc = DateTimeOffset.UtcNow.AddSeconds(60),
                DefaultChoiceId = "resupply",
                Choices = [new StarshipEventChoiceSnapshot { ChoiceId = "resupply", Label = "Resupply" }]
            }
        });
        var permanentBalance = 0L;
        var appliedOperations = new HashSet<string>(StringComparer.Ordinal);
        var points = new Mock<IPointsSystem>();
        var triggerService = new Mock<IDefaultCommandTriggerService>();
        triggerService.Setup(trigger => trigger.TriggerDefaultCommandEventAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CommandEventArgs>(), It.IsAny<Dictionary<string, string>>()))
            .Returns(Task.CompletedTask);
        points.Setup(system => system.AddPointsByUserIdOnce("pilot-id", 3, 25, It.IsAny<string>()))
            .Returns((string _, int _, long amount, string operationId) =>
            {
                if (appliedOperations.Add(operationId)) permanentBalance += amount;
                return Task.FromResult(true);
            });
        var service = CreateService(stateStore, settingsStore: new InMemorySettingsStore(),
            defaultCommandTriggerService: triggerService.Object, pointsSystem: points.Object);
        stateStore.FailOnSaveNumber = stateStore.SaveCount + 2;

        await Assert.ThrowsAsync<IOException>(() => service.AdvanceEventAsync());
        Assert.Equal(25, permanentBalance);
        Assert.False((await service.GetSnapshotAsync())!.BankedPayouts.Single().IsPaid);

        await service.ProcessDueTransitionAsync();

        Assert.Equal(25, permanentBalance);
        Assert.True((await service.GetSnapshotAsync())!.BankedPayouts.Single().IsPaid);
        Assert.Single(appliedOperations);
        points.Verify(system => system.AddPointsByUserIdOnce("pilot-id", 3, 25, It.IsAny<string>()), Times.Exactly(2));
        triggerService.Verify(trigger => trigger.TriggerDefaultCommandEventAsync(
            StarshipExpeditionService.DefaultCommandName,
            DefaultCommandEventTypes.StarshipCrewRewardBanked,
            It.Is<CommandEventArgs>(args => args.UserId == "pilot-id"),
            It.Is<Dictionary<string, string>>(variables => variables["Amount"] == "25")), Times.Once);
    }

    [Fact]
    public async Task StartExpedition_DoesNotReplaceSnapshotWhileStationPayoutIsPending()
    {
        var stateStore = new InMemoryStateStore();
        var priorExpeditionId = Guid.NewGuid();
        await stateStore.SaveAsync(new StarshipExpeditionSnapshot
        {
            ExpeditionId = priorExpeditionId,
            Status = StarshipExpeditionStatus.Returned,
            StartedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            LegCount = 1,
            CurrentLeg = 1,
            BankedPayouts =
            [
                new StarshipBankedPayoutSnapshot
                {
                    OperationId = "pending-payout", EventId = Guid.NewGuid(), UserId = "viewer-id",
                    PointTypeId = 1, Amount = 10
                }
            ]
        });
        var service = CreateService(stateStore);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartExpeditionAsync("moderator", seed: 99));

        Assert.Equal(priorExpeditionId, (await service.GetSnapshotAsync())?.ExpeditionId);
        Assert.False((await service.GetSnapshotAsync())!.BankedPayouts.Single().IsPaid);
    }

    [Fact]
    public async Task FinalReturn_BanksUnbankedCrewBalanceThroughIdempotentPointsPath()
    {
        var stateStore = new InMemoryStateStore();
        await stateStore.SaveAsync(new StarshipExpeditionSnapshot
        {
            ExpeditionId = Guid.NewGuid(),
            Seed = 91,
            StartedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Status = StarshipExpeditionStatus.EventOpen,
            CurrentLeg = 0,
            LegCount = 1,
            CurrentSector = "Kepler Reach",
            CurrentRoute = "Direct Route",
            SectorPool = ["Kepler Reach", "Ternion Belt"],
            ActiveCrewRewardsOnly = false,
            Crew =
            [
                new StarshipCrewMemberSnapshot
                {
                    UserId = "pilot-id", UserName = "pilot", DisplayName = "Pilot",
                    UnbankedBalances = [new StarshipUnbankedBalanceSnapshot { PointTypeId = 8, PointTypeName = "Credits", Amount = 35 }]
                }
            ],
            CurrentEvent = new StarshipExpeditionEventSnapshot
            {
                EventId = Guid.NewGuid(),
                Sequence = 1,
                EventType = "RouteChoice",
                Title = "Choose a Route",
                Sector = "Kepler Reach",
                OpenedAtUtc = DateTimeOffset.UtcNow,
                ClosesAtUtc = DateTimeOffset.UtcNow.AddSeconds(60),
                Choices = [new StarshipEventChoiceSnapshot { ChoiceId = "direct", Label = "Direct route" }],
                DefaultChoiceId = "direct"
            }
        });
        var points = new Mock<IPointsSystem>();
        points.Setup(system => system.AddPointsByUserIdOnce("pilot-id", 8, 35, It.IsAny<string>())).ReturnsAsync(true);
        var service = CreateService(stateStore, settingsStore: new InMemorySettingsStore(), pointsSystem: points.Object);

        var returned = await service.AdvanceEventAsync();

        Assert.Equal(StarshipExpeditionStatus.Returned, returned.Status);
        var payout = Assert.Single(returned.BankedPayouts);
        Assert.True(payout.IsPaid);
        Assert.Equal(35, payout.Amount);
        points.Verify(system => system.AddPointsByUserIdOnce("pilot-id", 8, 35, payout.OperationId), Times.Once);
    }

    [Fact]
    public async Task TierRewards_AggregateCurrenciesAndApplyIndependentCaps()
    {
        var snapshot = CreateSalvageFixture(0);
        var profile = snapshot.RewardProfiles[0];
        profile.SuccessChancePercent = 100;
        profile.ConvertLegacyToTiers();
        profile.Tiers[0].Rewards =
        [
            new() { PointTypeId = 2, PointTypeName = "Credits", Minimum = 20, Maximum = 20 },
            new() { PointTypeId = 2, PointTypeName = "Credits", Minimum = 30, Maximum = 30 },
            new() { PointTypeId = 3, PointTypeName = "Tokens", Minimum = 12, Maximum = 12 }
        ];
        profile.Caps =
        [
            new() { PointTypeId = 2, MaximumPerEvent = 40, MaximumUnbankedPerVoyage = 100 },
            new() { PointTypeId = 3, MaximumPerEvent = 10, MaximumUnbankedPerVoyage = 100 }
        ];
        var store = new InMemoryStateStore();
        await store.SaveAsync(snapshot);
        var service = CreateService(store);
        await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "salvage");
        await service.ConfirmRiskActionAsync(CreateArgs("pilot-id", "Pilot"));

        var resolved = await service.AdvanceEventAsync();

        var outcomes = Assert.Single(resolved.CompletedEvents).RewardOutcomes;
        Assert.Equal(40, outcomes.Single(item => item.PointTypeId == 2).EarnedAmount);
        Assert.Equal(10, outcomes.Single(item => item.PointTypeId == 3).EarnedAmount);
        Assert.Equal(2, resolved.Crew.Single().UnbankedBalances.Count);
    }

    [Fact]
    public async Task LegacyTierConversion_PreservesSeededPayouts()
    {
        var original = CreateSalvageFixture(0);
        original.RewardProfiles[0].SuccessChancePercent = 100;
        original.RewardProfiles[0].CommonMaximum = 50;
        var converted = JsonSerializer.Deserialize<StarshipExpeditionSnapshot>(JsonSerializer.Serialize(original))!;
        converted.RewardProfiles[0].ConvertLegacyToTiers();
        var originalStore = new InMemoryStateStore();
        var convertedStore = new InMemoryStateStore();
        await originalStore.SaveAsync(original);
        await convertedStore.SaveAsync(converted);
        var results = new List<string>();
        foreach (var store in new[] { originalStore, convertedStore })
        {
            var service = CreateService(store);
            await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "salvage");
            await service.ConfirmRiskActionAsync(CreateArgs("pilot-id", "Pilot"));
            var resolved = await service.AdvanceEventAsync();
            results.Add(JsonSerializer.Serialize(resolved.CompletedEvents.Single().RewardOutcomes));
        }

        Assert.Equal(results[0], results[1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RewardAction_ClaimsBeforeQueueingAndDoesNotReplayAfterRecovery(bool queueFails)
    {
        var snapshot = CreateSalvageFixture();
        snapshot.PendingActions.Add(new StarshipPendingActionSnapshot
        {
            OperationId = "reward-action-operation", ActionId = 42
        });
        var store = new InMemoryStateStore();
        await store.SaveAsync(snapshot);
        var actions = new Mock<IActionManagementService>();
        var action = new PenguinTwitchBot.Database.Bot.Actions.ActionType { Name = "Reward alert" };
        actions.Setup(manager => manager.GetActionByIdAsync(42)).ReturnsAsync(action);
        var runner = new Mock<IAction>();
        runner.Setup(instance => instance.EnqueueAction(It.IsAny<ConcurrentDictionary<string, string>>(), action, null, null))
            .Returns(async (ConcurrentDictionary<string, string> variables, PenguinTwitchBot.Database.Bot.Actions.ActionType _, Guid? _, int? _) =>
            {
                var saved = await store.LoadAsync();
                Assert.True(saved!.PendingActions.Single().Executed);
                Assert.Equal("reward-action-operation", variables["OperationId"]);
                if (queueFails) throw new IOException("Queue unavailable");
            });
        using var provider = new ServiceCollection()
            .AddSingleton(actions.Object).AddSingleton(runner.Object).BuildServiceProvider();
        var scopes = provider.GetRequiredService<IServiceScopeFactory>();

        await CreateService(store, scopeFactory: scopes).ProcessDueTransitionAsync();
        await CreateService(store, scopeFactory: scopes).ProcessDueTransitionAsync();

        runner.Verify(instance => instance.EnqueueAction(It.IsAny<ConcurrentDictionary<string, string>>(), action, null, null), Times.Once);
        Assert.Equal(1, (await store.LoadAsync())!.PendingActions.Single().Attempts);
    }

    [Fact]
    public async Task SharedProfileBinding_DrivesRewardsAndOverlayDisclosure()
    {
        var snapshot = CreateSalvageFixture(0);
        var profile = snapshot.RewardProfiles[0];
        profile.Id = "shared";
        profile.EventType = "SignalDetected";
        profile.SuccessChancePercent = 100;
        snapshot.EventRewardProfileIds["SalvageSite"] = "shared";
        var store = new InMemoryStateStore();
        await store.SaveAsync(snapshot);
        var service = CreateService(store);
        Assert.NotNull(StarshipExpeditionOverlaySnapshot.From(snapshot).CurrentEvent!.RewardDisclosure);

        await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "salvage");
        await service.ConfirmRiskActionAsync(CreateArgs("pilot-id", "Pilot"));
        var resolved = await service.AdvanceEventAsync();

        Assert.Equal(10, resolved.CompletedEvents.Single().RewardOutcomes.Single().EarnedAmount);
    }

    [Fact]
    public async Task AbortBanking_CoalescesAfterBankActionsAndWaitsForAllPayouts()
    {
        var snapshot = CreateSalvageFixture();
        snapshot.PendingActions.AddRange(Enumerable.Range(1, 2).Select(index => new StarshipPendingActionSnapshot
        {
            OperationId = $"after-bank-{index}", ActionId = 42, Timing = StarshipActionTimings.AfterBank
        }));
        var store = new InMemoryStateStore();
        await store.SaveAsync(snapshot);
        var points = new Mock<IPointsSystem>();
        var attempts = 0;
        points.Setup(system => system.AddPointsByUserIdOnce("pilot-id", 2, 100, It.IsAny<string>()))
            .ReturnsAsync(() => ++attempts > 1);
        var service = CreateService(store, pointsSystem: points.Object);

        var stopped = await service.StopExpeditionAsync("moderator");

        Assert.False(stopped!.BankedPayouts.Single().IsPaid);
        var pending = Assert.Single(stopped.PendingActions, item => !item.Executed);
        Assert.Equal("Aborted", pending.Variables["VoyageResult"]);
        Assert.Equal("100", pending.Variables["BankedTotal"]);
        Assert.NotNull(pending.BankEventId);
        await service.ProcessDueTransitionAsync();
        Assert.True((await service.GetSnapshotAsync())!.BankedPayouts.Single().IsPaid);
    }

    [Fact]
    public async Task CatalogSnapshot_CustomEventsUseProfilesAndIgnoreLaterSettingsEditsAfterRecovery()
    {
        var settings = new StarshipExpeditionSettings { LegCount = 5, StationIntervalLegs = 24, ActiveCrewRewardsOnly = false };
        var profile = new StarshipRewardProfile
        {
            Id = "archive-reward", Name = "Archive Reward", Enabled = true, SuccessChancePercent = 100,
            Tiers = [new() { Id = "rare", Name = "Rare", Weight = 1, Rewards = [new() { PointTypeId = 8, Minimum = 17, Maximum = 17 }] }]
        };
        settings.RewardProfiles.Add(profile);
        settings.EventCatalog = settings.EventCatalog.Where(definition => StarshipEventCatalog.IsStructural(definition.EventType)).ToList();
        settings.EventCatalog.Add(new StarshipEventDefinition("lost-archive", "Lost Archive", "Inspect {subject}.",
            [new() { ChoiceId = "recover", Label = "Recover the archive" }, new() { ChoiceId = "pass", Label = "Pass", EndsWithoutReward = true }], "recover", "Recovered {loot}.")
        {
            RewardProfileId = profile.Id, SubjectStyle = "custom", SubjectNames = ["the Quiet Archive"], ObjectKind = "beacon",
            Images = ["/media/archive.png"], LootByTier = new() { ["Rare"] = "an intact star atlas" }
        });
        var points = new Mock<IPointsSystem>();
        points.Setup(system => system.GetPointTypeById(8)).ReturnsAsync(new PointType { Id = 8, Name = "Archive Credits" });
        var settingsStore = new InMemorySettingsStore(settings);
        var stateStore = new InMemoryStateStore();
        var service = CreateService(stateStore, settingsStore: settingsStore, pointsSystem: points.Object);
        await service.StartExpeditionAsync("moderator", seed: 81);
        await service.JoinCrewAsync(CreateArgs("pilot-id", "Pilot"));
        await service.AdvanceEventAsync();
        await service.AdvanceEventAsync();

        settings.EventCatalog.Clear();
        profile.Tiers[0].Rewards[0].Minimum = 99;
        profile.Tiers[0].Rewards[0].Maximum = 99;
        var restored = CreateService(stateStore, settingsStore: settingsStore, pointsSystem: points.Object);
        var opened = await restored.AdvanceEventAsync();
        var expeditionEvent = opened.CurrentEvent!;
        Assert.Equal("lost-archive", expeditionEvent.EventType);
        Assert.Equal("the Quiet Archive", expeditionEvent.Subject);
        Assert.Equal(["/media/archive.png"], StarshipExpeditionOverlaySnapshot.From(opened).CurrentEvent!.ObjectImages);
        var resolved = await restored.AdvanceEventAsync();
        var completed = resolved.CompletedEvents.Last();
        Assert.Equal("an intact star atlas", completed.LootByTier!["Rare"]);
        Assert.Contains("an intact star atlas", completed.OutcomeStory);
        Assert.Equal(17, Assert.Single(completed.RewardOutcomes).EarnedAmount);
    }

    [Theory]
    [InlineData(null, null, 700)]
    [InlineData(150L, null, 150)]
    [InlineData(null, 600L, 100)]
    [InlineData(150L, 600L, 100)]
    public async Task OptionalPointLimits_EnforceOnlyExplicitEventAndVoyageCaps(long? eventLimit, long? voyageLimit, long expected)
    {
        var snapshot = CreateSalvageFixture(0);
        var profile = snapshot.RewardProfiles[0];
        profile.SuccessChancePercent = 100;
        profile.ConvertLegacyToTiers();
        profile.Tiers[0].Rewards[0].Minimum = profile.Tiers[0].Rewards[0].Maximum = 700;
        profile.Caps = [new() { PointTypeId = 2, MaximumPerEvent = eventLimit, MaximumUnbankedPerVoyage = voyageLimit }];
        snapshot.CompletedEvents.Add(new StarshipExpeditionEventSnapshot
        {
            RewardOutcomes = [new() { UserId = "pilot-id", PointTypeId = 2, EarnedAmount = 500 }]
        });
        var store = new InMemoryStateStore();
        await store.SaveAsync(snapshot);
        var service = CreateService(store);
        await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "salvage");
        await service.ConfirmRiskActionAsync(CreateArgs("pilot-id", "Pilot"));

        var resolved = await service.AdvanceEventAsync();

        Assert.Equal(expected, resolved.CompletedEvents.Last().RewardOutcomes.Single().EarnedAmount);
    }

    [Theory]
    [InlineData(null, 400)]
    [InlineData(0L, 0)]
    [InlineData(50L, 50)]
    public async Task OptionalLossCap_PreservesPercentageBalanceAndExplicitZero(long? maximum, long expected)
    {
        var snapshot = CreateSalvageFixture(1000);
        var profile = snapshot.RewardProfiles[0];
        profile.SuccessChancePercent = 0;
        profile.ConvertLegacyToTiers();
        profile.LossRules[0].MaximumPerEvent = maximum;
        var store = new InMemoryStateStore();
        await store.SaveAsync(snapshot);
        var service = CreateService(store);
        await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "salvage");
        await service.ConfirmRiskActionAsync(CreateArgs("pilot-id", "Pilot"));

        var resolved = await service.AdvanceEventAsync();

        Assert.Equal(expected, resolved.CompletedEvents.Last().RewardOutcomes.Single().LostAmount);
        Assert.Equal(1000 - expected, resolved.Crew.Single().UnbankedBalances.Single().Amount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MultiCurrencyOutcome_CountsDistinctParticipantsAndFiresOneRiskTrigger(bool succeeds)
    {
        var snapshot = CreateSalvageFixture();
        var profile = snapshot.RewardProfiles[0];
        profile.SuccessChancePercent = succeeds ? 100 : 0;
        profile.ConvertLegacyToTiers();
        profile.Tiers[0].Rewards.Add(new() { PointTypeId = 3, PointTypeName = "Tokens", Minimum = 10, Maximum = 10 });
        profile.LossRules.Add(new() { PointTypeId = 3, PointTypeName = "Tokens", Percent = 20, MaximumPerEvent = 100 });
        snapshot.Crew[0].UnbankedBalances.Add(new() { PointTypeId = 3, PointTypeName = "Tokens", Amount = 50 });
        var store = new InMemoryStateStore();
        await store.SaveAsync(snapshot);
        var fired = new List<(string EventType, Dictionary<string, string> Variables)>();
        var triggers = new Mock<IDefaultCommandTriggerService>();
        triggers.Setup(instance => instance.TriggerDefaultCommandEventAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CommandEventArgs>(), It.IsAny<Dictionary<string, string>>()))
            .Callback<string, string, CommandEventArgs, Dictionary<string, string>>((_, eventType, _, variables) => fired.Add((eventType, variables)))
            .Returns(Task.CompletedTask);
        var service = CreateService(store, defaultCommandTriggerService: triggers.Object);
        await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "salvage");
        await service.ConfirmRiskActionAsync(CreateArgs("pilot-id", "Pilot"));

        await service.AdvanceEventAsync();

        Assert.Equal(succeeds ? "1" : "0", fired.Single(item => item.EventType == DefaultCommandEventTypes.StarshipEventResolved).Variables["RewardParticipantCount"]);
        Assert.Equal("1", fired.Single(item => item.EventType == (succeeds ? DefaultCommandEventTypes.StarshipEventSucceeded : DefaultCommandEventTypes.StarshipEventFailed)).Variables["AffectedCount"]);
        var risk = Assert.Single(fired, item => item.EventType == (succeeds ? DefaultCommandEventTypes.StarshipRiskSucceeded : DefaultCommandEventTypes.StarshipRiskFailed));
        Assert.Equal(succeeds ? "20" : "0", risk.Variables["EarnedAmount"]);
        Assert.Equal(succeeds ? "0" : "50", risk.Variables["LostAmount"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Settlement_FailedPayoutDoesNotBlockLaterUsersAndRetriesNextPass(bool throws)
    {
        var snapshot = CreateSalvageFixture();
        snapshot.BankedPayouts.AddRange(
        [
            new() { OperationId = "first-operation", UserId = "first-user", PointTypeId = 2, Amount = 10 },
            new() { OperationId = "second-operation", UserId = "second-user", PointTypeId = 2, Amount = 20 }
        ]);
        var store = new InMemoryStateStore();
        await store.SaveAsync(snapshot);
        var points = new Mock<IPointsSystem>();
        var attempts = 0;
        points.Setup(instance => instance.AddPointsByUserIdOnce("first-user", 2, 10, "first-operation")).Returns(() =>
        {
            attempts++;
            return attempts == 1 && throws ? Task.FromException<bool>(new IOException("Payout unavailable")) : Task.FromResult(attempts > 1);
        });
        points.Setup(instance => instance.AddPointsByUserIdOnce("second-user", 2, 20, "second-operation")).ReturnsAsync(true);
        var service = CreateService(store, pointsSystem: points.Object);

        await service.ProcessDueTransitionAsync();
        var firstPass = await service.GetSnapshotAsync();
        Assert.False(firstPass!.BankedPayouts[0].IsPaid);
        Assert.True(firstPass.BankedPayouts[1].IsPaid);
        Assert.Equal(1, attempts);
        await service.ProcessDueTransitionAsync();
        Assert.All((await service.GetSnapshotAsync())!.BankedPayouts, payout => Assert.True(payout.IsPaid));
        points.Verify(instance => instance.AddPointsByUserIdOnce("second-user", 2, 20, "second-operation"), Times.Once);
    }

    [Theory]
    [InlineData("resupply", 40, 90, 0)]
    [InlineData("repair", 70, 35, 0)]
    [InlineData("continue", 40, 50, 10)]
    public async Task ShipStationChoices_ApplyDistinctEffectsAndBankEveryChoice(string choiceId, int expectedHull, int expectedSupplies, int expectedBonus)
    {
        var snapshot = CreateSalvageFixture(shipSystems: StarshipShipSystems.Create(new() { StartingHull = 40, StartingSupplies = 50 }));
        snapshot.CurrentEvent = new StarshipExpeditionEventSnapshot
        {
            EventId = Guid.NewGuid(), Sequence = 2, EventType = "StationStop", Title = "Station", DefaultChoiceId = "resupply",
            OpenedAtUtc = DateTimeOffset.UtcNow, ClosesAtUtc = DateTimeOffset.UtcNow.AddSeconds(60),
            Choices = [new() { ChoiceId = "resupply", Label = "Resupply" }, new() { ChoiceId = "repair", Label = "Repair" }, new() { ChoiceId = "continue", Label = "Continue" }]
        };
        var store = new InMemoryStateStore();
        await store.SaveAsync(snapshot);
        var points = new Mock<IPointsSystem>();
        points.Setup(instance => instance.AddPointsByUserIdOnce("pilot-id", 2, 100, It.IsAny<string>())).ReturnsAsync(true);
        var service = CreateService(store, pointsSystem: points.Object);
        await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), choiceId);

        var resolved = await service.AdvanceEventAsync();

        Assert.Equal(expectedHull, resolved.ShipSystems!.Hull);
        Assert.Equal(expectedSupplies, resolved.ShipSystems.Supplies);
        Assert.Equal(expectedBonus, resolved.ShipSystems.PendingSuccessBonusPercent);
        Assert.True(Assert.Single(resolved.BankedPayouts).IsPaid);
        await service.ProcessDueTransitionAsync();
        points.Verify(instance => instance.AddPointsByUserIdOnce("pilot-id", 2, 100, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ShipTravel_ChargesPerOpenedLegAndOmitsUnaffordableRepair()
    {
        var settings = new StarshipExpeditionSettings { LegCount = 5, StationIntervalLegs = 1, ShipSystems = new() { StartingSupplies = 10 } };
        var service = CreateService(new InMemoryStateStore(), settingsStore: new InMemorySettingsStore(settings));
        await service.StartExpeditionAsync("moderator", seed: 71);
        await service.JoinCrewAsync(CreateArgs("pilot-id", "Pilot"));
        var route = await service.AdvanceEventAsync();
        Assert.Equal(10, route.ShipSystems!.Supplies);
        await service.AdvanceEventAsync();

        var station = await service.AdvanceEventAsync();

        Assert.Equal(5, station.ShipSystems!.Supplies);
        Assert.Equal("StationStop", station.CurrentEvent!.EventType);
        Assert.DoesNotContain(station.CurrentEvent.Choices, choice => choice.ChoiceId == "repair");
        Assert.Contains(station.CurrentEvent.Choices, choice => choice.ChoiceId == station.CurrentEvent.DefaultChoiceId);
        await Assert.ThrowsAsync<ArgumentException>(() => service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "repair"));
    }

    [Theory]
    [InlineData(100, "Success", 100)]
    [InlineData(0, "Failure", 80)]
    public async Task ShipOutcome_ResolvesWithoutRewardProfileAndAppliesSharedDamage(int chance, string expectedOutcome, int expectedHull)
    {
        var fixture = CreateSalvageFixture(shipSystems: StarshipShipSystems.Create(new()));
        fixture.RewardProfiles.Clear();
        var expeditionEvent = fixture.CurrentEvent!;
        expeditionEvent.Choices = [new() { ChoiceId = "recover", Label = "Recover" }];
        expeditionEvent.ChoicePool = [.. expeditionEvent.Choices];
        expeditionEvent.ShipContext = new()
        {
            BaseSuccessChancePercent = chance, FailureEffect = new() { HullDelta = -20 },
            HullBefore = 100, SuppliesBefore = 100, HullAfter = 100, SuppliesAfter = 100
        };
        var store = new InMemoryStateStore();
        await store.SaveAsync(fixture);
        var service = CreateService(store);
        await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "recover");

        var resolved = await service.AdvanceEventAsync();

        Assert.Equal(expectedOutcome, resolved.CompletedEvents.Last().OutcomeResult);
        Assert.Equal(expectedHull, resolved.ShipSystems!.Hull);
        Assert.Empty(resolved.CompletedEvents.Last().RewardOutcomes);
        Assert.Equal(100, resolved.Crew.Single().UnbankedBalances.Single().Amount);
        Assert.DoesNotContain("caps reached", StarshipExpeditionNarrative.BuildEventMessage(resolved.CompletedEvents.Last()));
    }

    [Fact]
    public async Task ShipHullDepletion_ReturnsAndBanksOnlyOnceAcrossRecovery()
    {
        var fixture = CreateSalvageFixture(shipSystems: StarshipShipSystems.Create(new() { StartingHull = 10 }));
        fixture.RewardProfiles.Clear();
        fixture.CurrentEvent!.Choices = [new() { ChoiceId = "recover", Label = "Recover" }];
        fixture.CurrentEvent.ChoicePool = [.. fixture.CurrentEvent.Choices];
        fixture.CurrentEvent.ShipContext = new() { BaseSuccessChancePercent = 0, FailureEffect = new() { HullDelta = -20 }, HullBefore = 10 };
        var store = new InMemoryStateStore();
        await store.SaveAsync(fixture);
        var points = new Mock<IPointsSystem>();
        points.Setup(instance => instance.AddPointsByUserIdOnce("pilot-id", 2, 100, It.IsAny<string>())).ReturnsAsync(true);
        var triggers = new Mock<IDefaultCommandTriggerService>();
        triggers.Setup(instance => instance.TriggerDefaultCommandEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CommandEventArgs>(), It.IsAny<Dictionary<string, string>>())).Returns(Task.CompletedTask);
        var service = CreateService(store, pointsSystem: points.Object, defaultCommandTriggerService: triggers.Object);
        await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "recover");

        var returned = await service.AdvanceEventAsync();

        Assert.Equal(StarshipExpeditionStatus.Returned, returned.Status);
        Assert.Equal("HullDepleted", returned.ReturnReason);
        triggers.Verify(instance => instance.TriggerDefaultCommandEventAsync("starship", DefaultCommandEventTypes.StarshipVoyageEnded,
            It.IsAny<CommandEventArgs>(), It.Is<Dictionary<string, string>>(variables => variables["Hull"] == "0" && variables["ReturnReason"] == "HullDepleted")), Times.Once);
        triggers.Verify(instance => instance.TriggerDefaultCommandEventAsync("starship", DefaultCommandEventTypes.StarshipEventFailed,
            It.IsAny<CommandEventArgs>(), It.Is<Dictionary<string, string>>(variables => variables["HullBefore"] == "10" && variables["HullAfter"] == "0" && variables["HullChange"] == "-10")), Times.Once);
        Assert.Contains("returned early (hull depleted)", string.Join(" ", StarshipExpeditionNarrative.BuildVoyageLines(returned)));
        Assert.Equal(0, returned.ShipSystems!.Hull);
        Assert.Null(returned.NextEventAtUtc);
        Assert.True(Assert.Single(returned.BankedPayouts).IsPaid);
        await CreateService(store, pointsSystem: points.Object).ProcessDueTransitionAsync();
        points.Verify(instance => instance.AddPointsByUserIdOnce("pilot-id", 2, 100, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ShipMultiStep_ChargesTravelAndChoiceCostOnceAndRetainsBonusAfterRecovery()
    {
        var settings = new StarshipExpeditionSettings { LegCount = 5, StationIntervalLegs = 24 };
        settings.EventCatalog = settings.EventCatalog.Where(definition => StarshipEventCatalog.IsStructural(definition.EventType)).ToList();
        settings.EventCatalog.Add(new StarshipEventDefinition("ship-test", "Ship Test", "A cache.",
            [new() { ChoiceId = "scan", Label = "Scan", ContinuesEvent = true, GrantsFlag = "scanned", ShipEffect = new() { SuppliesCost = 10 } },
             new() { ChoiceId = "recover", Label = "Recover" }], "recover") { SuccessChancePercent = 100 });
        var settingsStore = new InMemorySettingsStore(settings);
        var store = new InMemoryStateStore();
        var service = CreateService(store, settingsStore: settingsStore);
        await service.StartExpeditionAsync("moderator", seed: 71);
        await service.JoinCrewAsync(CreateArgs("pilot-id", "Pilot"));
        await service.AdvanceEventAsync();
        await service.AdvanceEventAsync();
        var cruising = await service.GetSnapshotAsync();
        StarshipShipSystems.ApplyStationChoice(cruising!.ShipSystems!, "test-continue", "continue");
        await store.SaveAsync(cruising);
        service = CreateService(store, settingsStore: settingsStore);
        var opened = await service.AdvanceEventAsync();
        Assert.Equal(95, opened.ShipSystems!.Supplies);
        Assert.Equal(10, opened.CurrentEvent!.ShipContext!.ResourceSuccessModifierPercent);
        await service.VoteAsync(CreateArgs("pilot-id", "Pilot"), "scan");
        var stepped = await service.AdvanceEventAsync();
        Assert.Equal(85, stepped.ShipSystems!.Supplies);
        Assert.Equal(10, stepped.ShipSystems.ActiveSuccessBonusPercent);
        Assert.DoesNotContain(stepped.CurrentEvent!.Choices, choice => choice.ChoiceId == "scan");

        var recovered = CreateService(store, settingsStore: settingsStore);
        var finished = await recovered.AdvanceEventAsync();

        Assert.Equal(85, finished.ShipSystems!.Supplies);
        Assert.Null(finished.ShipSystems.ActiveBonusEventId);
        Assert.Equal("Success", finished.CompletedEvents.Last().OutcomeResult);
    }

    [Fact]
    public async Task ShipIntegration_LegacyFoundationSnapshotDoesNotGainStationEffects()
    {
        var ship = StarshipShipSystems.Create(new() { StartingHull = 40, StartingSupplies = 50 })!;
        var json = JsonSerializer.SerializeToNode(ship)!.AsObject();
        json.Remove(nameof(StarshipShipSystemsSnapshot.IntegrationEnabled));
        var legacy = JsonSerializer.Deserialize<StarshipShipSystemsSnapshot>(json.ToJsonString())!;
        Assert.False(legacy.IntegrationEnabled);
        var fixture = CreateSalvageFixture(shipSystems: legacy);
        fixture.CurrentEvent = new StarshipExpeditionEventSnapshot
        {
            EventId = Guid.NewGuid(), EventType = "StationStop", Sequence = 2, Title = "Station", DefaultChoiceId = "resupply",
            ClosesAtUtc = DateTimeOffset.UtcNow.AddSeconds(60), Choices = [new() { ChoiceId = "resupply", Label = "Resupply" }]
        };
        var store = new InMemoryStateStore();
        await store.SaveAsync(fixture);

        var resolved = await CreateService(store).AdvanceEventAsync();

        Assert.Equal(40, resolved.ShipSystems!.Hull);
        Assert.Equal(50, resolved.ShipSystems.Supplies);
        Assert.Empty(resolved.ShipSystems.AppliedOperationIds);
    }

    [Fact]
    public void ShipSuccessChance_UsesFrozenResourceContextAndTerminalChoiceModifier()
    {
        var expeditionEvent = new StarshipExpeditionEventSnapshot
        {
            ShipContext = new() { BaseSuccessChancePercent = 75, ResourceSuccessModifierPercent = -5 },
            SuccessChanceModifierPercent = 20
        };
        Assert.Equal(90, StarshipRewardResolver.GetEffectiveSuccessChance(null, expeditionEvent));
        Assert.Equal(75, StarshipRewardResolver.GetEffectiveSuccessChance(null, expeditionEvent, new() { SuccessChanceModifierPercent = -15 }));
    }

    [Fact]
    public async Task ShipChoice_SaveFailureRollsBackEffectAndRetryAppliesItOnce()
    {
        var fixture = CreateSalvageFixture(shipSystems: StarshipShipSystems.Create(new() { StartingHull = 40, StartingSupplies = 50 }));
        fixture.CurrentEvent = new StarshipExpeditionEventSnapshot
        {
            EventId = Guid.NewGuid(), Sequence = 2, EventType = "StationStop", Title = "Station", DefaultChoiceId = "resupply",
            ClosesAtUtc = DateTimeOffset.UtcNow.AddSeconds(60), Choices = [new() { ChoiceId = "resupply", Label = "Resupply" }]
        };
        var store = new InMemoryStateStore();
        await store.SaveAsync(fixture);
        var service = CreateService(store);
        store.FailNextSave = true;

        await Assert.ThrowsAsync<IOException>(() => service.AdvanceEventAsync());

        var rolledBack = await service.GetSnapshotAsync();
        Assert.Equal(50, rolledBack!.ShipSystems!.Supplies);
        Assert.Empty(rolledBack.ShipSystems.AppliedOperationIds);
        var retried = await service.AdvanceEventAsync();
        Assert.Equal(90, retried.ShipSystems!.Supplies);
        Assert.Single(retried.ShipSystems.AppliedOperationIds);
    }

    [Fact]
    public async Task ShipChoice_StaleUnaffordableVoteUsesSafeDefaultWithoutEnrollingRejectedViewer()
    {
        var fixture = CreateSalvageFixture(shipSystems: StarshipShipSystems.Create(new() { StartingSupplies = 5 }));
        fixture.CurrentEvent = new StarshipExpeditionEventSnapshot
        {
            EventId = Guid.NewGuid(), Sequence = 2, EventType = "StationStop", Title = "Station", DefaultChoiceId = "resupply",
            ClosesAtUtc = DateTimeOffset.UtcNow.AddSeconds(60),
            Choices = [new() { ChoiceId = "resupply", Label = "Resupply" }, new() { ChoiceId = "repair", Label = "Repair" }],
            Votes = [new() { UserId = "pilot-id", ChoiceId = "repair" }]
        };
        var store = new InMemoryStateStore();
        await store.SaveAsync(fixture);
        var service = CreateService(store);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.VoteAsync(CreateArgs("new-viewer", "New Viewer"), "repair"));
        Assert.Single((await service.GetSnapshotAsync())!.Crew);

        var resolved = await service.AdvanceEventAsync();

        Assert.Equal("resupply", resolved.CompletedEvents.Last().ResolvedChoiceId);
        Assert.Equal(45, resolved.ShipSystems!.Supplies);
    }

    [Fact]
    public void ShipOverlayDisclosure_IncludesUnavailableRepairWithoutLeakingOperationLedger()
    {
        var snapshot = CreateSalvageFixture(shipSystems: StarshipShipSystems.Create(new() { StartingHull = 90, StartingSupplies = 10 }));
        snapshot.ShipSystems!.AppliedOperationIds.Add("private-operation");
        var pool = new List<StarshipEventChoiceSnapshot>
        {
            new() { ChoiceId = "resupply", Label = "Resupply" }, new() { ChoiceId = "repair", Label = "Repair" }, new() { ChoiceId = "continue", Label = "Continue" }
        };
        snapshot.CurrentEvent = new() { EventType = "StationStop", ChoicePool = pool, Choices = [pool[0], pool[2]], DefaultChoiceId = "resupply" };

        var projection = StarshipExpeditionOverlaySnapshot.From(snapshot);

        Assert.Equal(90, projection.ShipSystems!.Hull);
        var choices = projection.CurrentEvent!.ShipDisclosure!.Choices;
        var repair = choices.Single(choice => choice.ChoiceId == "repair");
        Assert.False(repair.Available);
        Assert.Null(repair.Number);
        Assert.Equal(15, repair.SuppliesCost);
        Assert.Equal(40, choices.Single(choice => choice.ChoiceId == "resupply").SuppliesDelta);
        Assert.Equal(2, choices.Single(choice => choice.ChoiceId == "continue").Number);
        Assert.Equal(10, choices.Single(choice => choice.ChoiceId == "continue").NextEventSuccessBonusPercent);
        Assert.DoesNotContain("private-operation", JsonSerializer.Serialize(projection));
    }

    [Fact]
    public void ShipOverlayDisclosure_UsesFrozenChanceAndSharedFailureScaling()
    {
        var snapshot = CreateSalvageFixture(shipSystems: StarshipShipSystems.Create(new() { StartingHull = 50, StartingSupplies = 40 }));
        var choice = new StarshipEventChoiceSnapshot { ChoiceId = "recover", Label = "Recover", ShipEffect = new() { SuppliesCost = 10 }, RiskMultiplierPercent = 50 };
        snapshot.CurrentEvent!.Choices = [choice];
        snapshot.CurrentEvent.ChoicePool = [choice];
        snapshot.CurrentEvent.ShipContext = new() { BaseSuccessChancePercent = 75, ResourceSuccessModifierPercent = 10, FailureEffect = new() { HullDelta = -20, SuppliesDelta = -10 } };
        var disclosure = StarshipShipEventDisclosure.From(snapshot, snapshot.CurrentEvent, null)!;
        var details = Assert.Single(disclosure.Choices);
        Assert.Equal(85, details.SuccessChancePercent);
        Assert.Equal(-10, details.SuppliesDelta);
        Assert.Equal(-10, details.FailureHullDelta);
        Assert.Equal(-5, details.FailureSuppliesDelta);
    }

    [Fact]
    public void ShipOverlayDisclosure_UsesChoiceModifierAndReportsNetSuppliesAfterCost()
    {
        var snapshot = CreateSalvageFixture(shipSystems: StarshipShipSystems.Create(new() { StartingSupplies = 20 }));
        var choice = new StarshipEventChoiceSnapshot { ChoiceId = "brace", Label = "Brace", SuccessChanceModifierPercent = 20 };
        snapshot.CurrentEvent!.Choices = [choice];
        snapshot.CurrentEvent.ChoicePool = [choice];
        snapshot.CurrentEvent.ShipContext = new()
        {
            BaseSuccessChancePercent = 70, SuppliesBefore = 20, SuppliesAfter = 20,
            FailureEffect = new() { SuppliesDelta = -5 }
        };
        var disclosure = StarshipShipEventDisclosure.From(snapshot, snapshot.CurrentEvent, null)!;
        Assert.Equal(1, Assert.Single(disclosure.Choices).Number);
        Assert.Equal(90, disclosure.Choices[0].SuccessChancePercent);

        snapshot.CurrentEvent.Choices[0] = new() { ChoiceId = choice.ChoiceId, Label = choice.Label, SuccessChanceModifierPercent = choice.SuccessChanceModifierPercent, ShipEffect = new() { SuppliesCost = 5 } };
        snapshot.CurrentEvent.ChoicePool = [.. snapshot.CurrentEvent.Choices];
        var paid = StarshipShipEventDisclosure.From(snapshot, snapshot.CurrentEvent, null)!.Choices[0];
        Assert.Equal(-5, paid.SuppliesDelta);
        Assert.Equal(5, paid.SuppliesCost);
        Assert.Equal("Supplies net -5; Cost 5 supplies; 90% success; Failure: hull 0, supplies -5", paid.Describe());
    }

    private static StarshipExpeditionService CreateService(
        InMemoryStateStore stateStore,
        TimeProvider? timeProvider = null,
        InMemorySettingsStore? settingsStore = null,
        Mock<IServiceBackbone>? serviceBackboneMock = null,
        IDefaultCommandTriggerService? defaultCommandTriggerService = null,
        Mock<IViewerFeature>? viewerFeatureMock = null,
        IPointsSystem? pointsSystem = null,
        List<string>? broadcasts = null,
        IServiceScopeFactory? scopeFactory = null)
    {
        var serviceBackbone = serviceBackboneMock ?? new Mock<IServiceBackbone>();
        if (serviceBackboneMock == null)
        {
            serviceBackbone.Setup(backbone => backbone.SendChatMessage(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            serviceBackbone.Setup(backbone => backbone.SendChatMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(Task.CompletedTask);
        }
        var webSocketMessenger = new Mock<IWebSocketMessenger>();
        webSocketMessenger.Setup(messenger => messenger.AddToQueue(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, payload) => broadcasts?.Add(payload))
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
            defaultCommandTriggerService,
            pointsSystem,
            viewerFeatureMock?.Object,
            scopeFactory);
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
        public int SaveCount { get; private set; }
        public int? FailOnSaveNumber { get; set; }

        public Task<StarshipExpeditionSnapshot?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_snapshotJson == null
                ? null
                : JsonSerializer.Deserialize<StarshipExpeditionSnapshot>(_snapshotJson));

        public Task SaveAsync(StarshipExpeditionSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            if (FailNextSave || SaveCount == FailOnSaveNumber)
            {
                FailNextSave = false;
                FailOnSaveNumber = null;
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
