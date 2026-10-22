using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Test.Bot.Commands.Games;

public class StarshipExpeditionStateStoreTests
{
    [Fact]
    public async Task ShipSystems_SaveReloadPreservesResourcesRulesBonusesAndAppliedOperations()
    {
        var rows = new List<Setting>();
        var state = StarshipShipSystems.Create(new() { StartingHull = 60, StartingSupplies = 50 })!;
        StarshipShipSystems.ApplyTravel(state, "leg-one");
        StarshipShipSystems.ApplyStationChoice(state, "station-one", "continue");
        var eventId = Guid.NewGuid();
        StarshipShipSystems.BeginEvent(state, eventId, isStation: false);
        var snapshot = new StarshipExpeditionSnapshot { ExpeditionId = Guid.NewGuid(), LegCount = 4, ShipSystems = state };

        await CreateStore(rows).SaveAsync(snapshot);
        var loaded = await CreateStore(rows).LoadAsync();

        var restored = loaded!.ShipSystems!;
        Assert.Equal(60, restored.Hull);
        Assert.Equal(40, restored.Supplies);
        Assert.Equal(eventId, restored.ActiveBonusEventId);
        Assert.Equal(10, restored.ActiveSuccessBonusPercent);
        Assert.Equal(10, StarshipShipSystems.BeginEvent(restored, eventId, isStation: false));
        Assert.Equal(StarshipShipEffectResult.AlreadyApplied, StarshipShipSystems.ApplyTravel(restored, "leg-one"));
        Assert.Equal(40, restored.Supplies);
        Assert.Equal(30, restored.Rules.StationRepairAmount);
    }

    [Fact]
    public async Task ShipSystems_LegacySnapshotRemainsWithoutNewMechanics()
    {
        var rows = new List<Setting>();
        var snapshot = new StarshipExpeditionSnapshot { ExpeditionId = Guid.NewGuid(), LegCount = 4 };
        await CreateStore(rows).SaveAsync(snapshot);
        var json = System.Text.Json.Nodes.JsonNode.Parse(rows.Single().StringSetting!)!.AsObject();
        json.Remove("shipSystems");
        rows.Single().StringSetting = json.ToJsonString();

        var loaded = await CreateStore(rows).LoadAsync();

        Assert.Null(loaded!.ShipSystems);
    }

    [Fact]
    public async Task Load_InvalidPersistedShipRulesDeletesSnapshotAndReturnsNoActiveVoyage()
    {
        var rows = new List<Setting>
        {
            new()
            {
                Name = StarshipExpeditionStateStore.SettingKey,
                DataType = Setting.DataTypeEnum.String,
                StringSetting = JsonSerializer.Serialize(new StarshipExpeditionSnapshot
                {
                    ExpeditionId = Guid.NewGuid(),
                    LegCount = 4,
                    Crew = null!,
                    BankedPayouts = null!,
                    ShipSystems = new StarshipShipSystemsSnapshot
                    {
                        IntegrationEnabled = true,
                        Rules = new() { MaximumHull = 0 },
                        Hull = 0,
                        Supplies = 20
                    }
                })
            }
        };
        var store = CreateStore(rows);

        var loaded = await store.LoadAsync();

        Assert.Null(loaded);
        Assert.Empty(rows);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Load_InvalidPersistedShipRulesPreservesSnapshotWithOutstandingRewards(bool hasUnbankedBalance, bool hasUnpaidPayout)
    {
        var snapshot = new StarshipExpeditionSnapshot
        {
            ExpeditionId = Guid.NewGuid(),
            LegCount = 4,
            Crew = hasUnbankedBalance ?
            [
                new StarshipCrewMemberSnapshot
                {
                    UserId = "viewer-1",
                    UnbankedBalances = [new StarshipUnbankedBalanceSnapshot { PointTypeId = 1, Amount = 20 }]
                }
            ] : [],
            BankedPayouts = hasUnpaidPayout ?
            [
                new StarshipBankedPayoutSnapshot
                {
                    OperationId = "starship:pending-payout", UserId = "viewer-1", PointTypeId = 1, Amount = 30
                }
            ] : [],
            ShipSystems = new StarshipShipSystemsSnapshot
            {
                IntegrationEnabled = true,
                Rules = new() { MaximumHull = 0 },
                Hull = 0,
                Supplies = 20
            }
        };
        var rows = new List<Setting>
        {
            new()
            {
                Name = StarshipExpeditionStateStore.SettingKey,
                DataType = Setting.DataTypeEnum.String,
                StringSetting = JsonSerializer.Serialize(snapshot)
            }
        };

        var loaded = await CreateStore(rows).LoadAsync();

        Assert.NotNull(loaded);
        Assert.Single(rows);
        Assert.Equal(hasUnbankedBalance, loaded.Crew.Any(member => member.UnbankedBalances.Any(balance => balance.Amount > 0)));
        Assert.Equal(hasUnpaidPayout, loaded.BankedPayouts.Any(payout => !payout.IsPaid));
    }

    [Fact]
    public async Task ShipSystems_SaveRejectsInvalidResourcesAndDuplicateOperations()
    {
        var state = StarshipShipSystems.Create(new())!;
        var snapshot = new StarshipExpeditionSnapshot { ExpeditionId = Guid.NewGuid(), LegCount = 4, ShipSystems = state };
        state.Hull = -1;
        await Assert.ThrowsAsync<InvalidDataException>(() => CreateStore([]).SaveAsync(snapshot));
        state.Hull = 100;
        state.AppliedOperationIds.AddRange(["same", "same"]);
        await Assert.ThrowsAsync<InvalidDataException>(() => CreateStore([]).SaveAsync(snapshot));
    }

    [Fact]
    public async Task Save_RejectsMalformedShipEventContext()
    {
        var snapshot = new StarshipExpeditionSnapshot
        {
            ExpeditionId = Guid.NewGuid(), LegCount = 4,
            CurrentEvent = new()
            {
                EventId = Guid.NewGuid(), EventType = "custom", Sequence = 1,
                ShipContext = new() { BaseSuccessChancePercent = 101 }
            }
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => CreateStore([]).SaveAsync(snapshot));
    }

    [Fact]
    public async Task SaveAndLoad_PreservesMissionSeedCrewAndOpenEvent()
    {
        var settingRows = new List<Setting>();
        var store = CreateStore(settingRows);
        var snapshot = new StarshipExpeditionSnapshot
        {
            ExpeditionId = Guid.NewGuid(),
            Seed = 127430,
            StartedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            LegCount = 5,
            CurrentLeg = 2,
            NextEventSequence = 4,
            NextEventAtUtc = DateTimeOffset.UtcNow.AddMinutes(5),
            CurrentSector = "Kepler Reach",
            CurrentRoute = "Debris Corridor",
            Crew =
            [
                new StarshipCrewMemberSnapshot
                {
                    UserId = "viewer-1",
                    UserName = "pilot",
                    DisplayName = "Pilot",
                    JoinedAtUtc = DateTimeOffset.UtcNow,
                    EnrollmentMethod = StarshipCrewEnrollmentMethod.ExplicitJoin,
                    UnbankedBalances = [new StarshipUnbankedBalanceSnapshot { PointTypeId = 2, PointTypeName = "Credits", Amount = 40 }]
                },
                new StarshipCrewMemberSnapshot
                {
                    UserId = "viewer-2",
                    UserName = "scanner",
                    DisplayName = "Scanner",
                    JoinedAtUtc = DateTimeOffset.UtcNow,
                    EnrollmentMethod = StarshipCrewEnrollmentMethod.AutomaticAction
                }
            ],
            CurrentEvent = new StarshipExpeditionEventSnapshot
            {
                EventId = Guid.NewGuid(),
                Sequence = 3,
                EventType = "SignalDetected",
                Title = "Signal",
                OpenedAtUtc = DateTimeOffset.UtcNow,
                ClosesAtUtc = DateTimeOffset.UtcNow.AddSeconds(30),
                Choices = [new StarshipEventChoiceSnapshot { ChoiceId = "scan", Label = "Scan" }],
                EligibleRewardUserIds = ["viewer-1"],
                RewardOutcomes = [new StarshipRewardOutcomeSnapshot
                {
                    UserId = "viewer-1", DisplayName = "Pilot", PointTypeId = 2, PointTypeName = "Credits",
                    Outcome = "Success", Rarity = "Common", EarnedAmount = 10
                }],
                PendingRiskConfirmations = [new StarshipRiskConfirmationSnapshot
                {
                    UserId = "viewer-1", ChoiceId = "scan", ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(20),
                    PointTypeId = 2, PointTypeName = "Credits", ExposedBalance = 40, MaximumLoss = 10
                }],
                Votes = [new StarshipEventVoteSnapshot { UserId = "viewer-1", ChoiceId = "scan" }]
            },
            RewardProfiles =
            [
                new StarshipRewardProfile
                {
                    EventType = "SignalDetected", Enabled = true, PointTypeId = 2, PointTypeName = "Credits",
                    SuccessChancePercent = 75, CommonWeightPercent = 70, UncommonWeightPercent = 25, RareWeightPercent = 5
                }
            ],
            BankedPayouts =
            [
                new StarshipBankedPayoutSnapshot
                {
                    OperationId = "starship:bank-1", EventId = Guid.NewGuid(), UserId = "viewer-1",
                    DisplayName = "Pilot", PointTypeId = 2, PointTypeName = "Credits", Amount = 40
                }
            ]
        };

        await store.SaveAsync(snapshot);
        var restored = await CreateStore(settingRows).LoadAsync();

        Assert.NotNull(restored);
        Assert.Equal(snapshot.ExpeditionId, restored.ExpeditionId);
        Assert.Equal(snapshot.Seed, restored.Seed);
        Assert.Equal(snapshot.CurrentLeg, restored.CurrentLeg);
        Assert.Equal(snapshot.NextEventSequence, restored.NextEventSequence);
        Assert.Collection(
            restored.Crew.OrderBy(member => member.UserId),
            member => Assert.Equal("viewer-1", member.UserId),
            member => Assert.Equal("viewer-2", member.UserId));
        Assert.Equal("SignalDetected", restored.CurrentEvent?.EventType);
        Assert.Equal("viewer-1", Assert.Single(restored.CurrentEvent!.Votes).UserId);
        Assert.Equal(40, Assert.Single(restored.Crew[0].UnbankedBalances).Amount);
        Assert.Equal(75, Assert.Single(restored.RewardProfiles).SuccessChancePercent);
        Assert.Equal(10, Assert.Single(restored.CurrentEvent.RewardOutcomes).EarnedAmount);
        Assert.Equal(10, Assert.Single(restored.CurrentEvent.PendingRiskConfirmations).MaximumLoss);
        Assert.Equal("starship:bank-1", Assert.Single(restored.BankedPayouts).OperationId);
        Assert.Single(settingRows);
    }

    [Fact]
    public async Task Save_RejectsDuplicateCrewUserIds()
    {
        var store = CreateStore([]);
        var snapshot = new StarshipExpeditionSnapshot
        {
            ExpeditionId = Guid.NewGuid(),
            LegCount = 1,
            Crew =
            [
                new StarshipCrewMemberSnapshot { UserId = "viewer-1" },
                new StarshipCrewMemberSnapshot { UserId = "VIEWER-1" }
            ]
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(snapshot));
    }

    [Fact]
    public void OverlayProjection_ExposesVoteCountsWithoutViewerIdentifiers()
    {
        var snapshot = new StarshipExpeditionSnapshot
        {
            ExpeditionId = Guid.NewGuid(),
            Status = StarshipExpeditionStatus.EventOpen,
            CurrentLeg = 1,
            LegCount = 4,
            RewardProfiles =
            [
                new StarshipRewardProfile
                {
                    EventType = "SignalDetected", Enabled = true, PointTypeId = 4, PointTypeName = "Credits",
                    SuccessChancePercent = 75, CommonWeightPercent = 70, UncommonWeightPercent = 25, RareWeightPercent = 5,
                    CommonMinimum = 5, CommonMaximum = 10, MaximumRiskLossPerEvent = 20
                }
            ],
            CompletedEvents =
            [
                new StarshipExpeditionEventSnapshot
                {
                    EventId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Sequence = 1, EventType = "StationStop", Title = "Space Station", Sector = "Kepler Reach",
                    Choices = [new StarshipEventChoiceSnapshot { ChoiceId = "bank", Label = "Bank the haul" }],
                    ResolvedChoiceId = "bank", ResolvedAtUtc = DateTimeOffset.UtcNow
                }
            ],
            BankedPayouts =
            [
                new StarshipBankedPayoutSnapshot
                {
                    OperationId = "starship:bank-op", EventId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    UserId = "private-viewer-id", DisplayName = "Pilot", PointTypeId = 4, PointTypeName = "Credits", Amount = 15, IsPaid = true
                }
            ],
            Crew = [new StarshipCrewMemberSnapshot { UserId = "private-viewer-id" }],
            CurrentEvent = new StarshipExpeditionEventSnapshot
            {
                EventId = Guid.NewGuid(),
                Sequence = 2,
                EventType = "SignalDetected",
                Title = "Unidentified Signal",
                Sector = "Kepler Reach",
                Choices = [
                    new StarshipEventChoiceSnapshot { ChoiceId = "scan", Label = "Scan the signal" },
                    new StarshipEventChoiceSnapshot { ChoiceId = "ignore", Label = "Continue on course" }
                ],
                Votes = [new StarshipEventVoteSnapshot { UserId = "private-viewer-id", ChoiceId = "scan" }]
            }
        };

        var projection = StarshipExpeditionOverlaySnapshot.From(snapshot);
        var serialized = JsonSerializer.Serialize(projection);

        Assert.Equal("EventOpen", projection.Status);
        Assert.Equal(1, projection.CrewCount);
        Assert.Equal(1, projection.CurrentEvent!.VoteCounts["scan"]);
        Assert.Contains(projection.CurrentEvent.Choices, choice => choice.Label == "Scan the signal");
        Assert.Equal(75, projection.CurrentEvent.RewardDisclosure!.SuccessChancePercent);
        Assert.Equal(15, Assert.Single(projection.LatestResult!.BankedAmounts).Amount);
        Assert.DoesNotContain("private-viewer-id", serialized, StringComparison.Ordinal);
    }

    private static StarshipExpeditionStateStore CreateStore(List<Setting> settingRows)
    {
        var settingsRepository = new Mock<ISettingsRepository>();
        settingsRepository.Setup(repository => repository.GetAsync(
                It.IsAny<Expression<Func<Setting, bool>>>(),
                It.IsAny<Func<IQueryable<Setting>, IOrderedQueryable<Setting>>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string>()))
            .ReturnsAsync(() => settingRows.ToList());
        settingsRepository.Setup(repository => repository.AddAsync(It.IsAny<Setting>()))
            .Callback<Setting>(setting => settingRows.Add(setting))
            .Returns((Setting _) => default);
        settingsRepository.Setup(repository => repository.Update(It.IsAny<Setting>()))
            .Callback<Setting>(setting =>
            {
                var index = settingRows.FindIndex(existing => existing.Name == setting.Name);
                if (index >= 0) settingRows[index] = setting;
            });
        settingsRepository.Setup(repository => repository.Remove(It.IsAny<Setting>()))
            .Callback<Setting>(setting => settingRows.Remove(setting));

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(work => work.Settings).Returns(settingsRepository.Object);
        unitOfWork.Setup(work => work.SaveChangesAsync()).ReturnsAsync(1);

        var provider = new Mock<IServiceProvider>();
        provider.Setup(serviceProvider => serviceProvider.GetService(typeof(IUnitOfWork))).Returns(unitOfWork.Object);

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(serviceScope => serviceScope.ServiceProvider).Returns(provider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(factory => factory.CreateScope()).Returns(scope.Object);

        return new StarshipExpeditionStateStore(scopeFactory.Object);
    }
}