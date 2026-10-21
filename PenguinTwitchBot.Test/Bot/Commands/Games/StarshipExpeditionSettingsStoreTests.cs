using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Test.Bot.Commands.Games;

public class StarshipExpeditionSettingsStoreTests
{
    [Fact]
    public async Task SaveAndLoad_PreservesSettingsAndNormalizedCustomSectors()
    {
        var rows = new List<Setting>();
        var store = CreateStore(rows);
        var settings = new StarshipExpeditionSettings
        {
            LegCount = 8,
            MinimumEventIntervalMinutes = 20,
            MaximumEventIntervalMinutes = 35,
            BoardingDurationSeconds = 45,
            ActiveCrewRewardsOnly = false,
            AnnounceSessionStatusInChat = false,
            SessionStartChatMessageTemplate = "Boarding {duration}s",
            SessionEndChatMessageTemplate = "Returned {count} in {duration}s",
            RewardProfiles =
            [
                new StarshipRewardProfile
                {
                    EventType = "SalvageSite",
                    Enabled = true,
                    PointTypeId = 7,
                    PointTypeName = "Salvage Credits",
                    SuccessChancePercent = 75,
                    CommonWeightPercent = 60,
                    UncommonWeightPercent = 30,
                    RareWeightPercent = 10,
                    CommonMinimum = 5,
                    CommonMaximum = 12,
                    RiskLossPercent = 25,
                    MaximumRiskLossPerEvent = 40
                }
            ],
            CustomSectors = ["  Delta Reach  ", "Glass Nebula"]
        };

        await store.SaveAsync(settings);
        var loaded = await CreateStore(rows).LoadAsync();

        Assert.Equal(8, loaded.LegCount);
        Assert.Equal(20, loaded.MinimumEventIntervalMinutes);
        Assert.Equal(35, loaded.MaximumEventIntervalMinutes);
        Assert.Equal(45, loaded.BoardingDurationSeconds);
        Assert.False(loaded.ActiveCrewRewardsOnly);
        Assert.False(loaded.AnnounceSessionStatusInChat);
        Assert.Equal("Boarding {duration}s", loaded.SessionStartChatMessageTemplate);
        Assert.Equal("Returned {count} in {duration}s", loaded.SessionEndChatMessageTemplate);
        var rewardProfile = loaded.RewardProfiles.Single(profile => profile.EventType == "SalvageSite");
        Assert.Equal("SalvageSite", rewardProfile.EventType);
        Assert.Equal(7, rewardProfile.PointTypeId);
        Assert.Equal(75, rewardProfile.SuccessChancePercent);
        Assert.Equal([60, 30, 10], new[] { rewardProfile.CommonWeightPercent, rewardProfile.UncommonWeightPercent, rewardProfile.RareWeightPercent });
        Assert.Equal(40, rewardProfile.MaximumRiskLossPerEvent);
        Assert.Equal(["Delta Reach", "Glass Nebula"], loaded.CustomSectors);
        Assert.Equal(Setting.DataTypeEnum.String, Assert.Single(rows).DataType);
    }

    [Fact]
    public async Task Load_WhenNoSavedRow_ReturnsDefaults()
    {
        var settings = await CreateStore([]).LoadAsync();

        Assert.Equal(StarshipExpeditionService.DefaultLegCount, settings.LegCount);
        Assert.Equal(StarshipExpeditionService.DefaultMinimumEventIntervalMinutes, settings.MinimumEventIntervalMinutes);
        Assert.Equal(StarshipExpeditionService.DefaultMaximumEventIntervalMinutes, settings.MaximumEventIntervalMinutes);
        Assert.Equal(StarshipExpeditionService.DefaultBoardingDurationSeconds, settings.BoardingDurationSeconds);
        Assert.True(settings.ActiveCrewRewardsOnly);
        Assert.True(settings.AnnounceSessionStatusInChat);
        Assert.All(settings.RewardProfiles, profile => Assert.False(profile.Enabled));
        Assert.Empty(settings.CustomSectors);
        Assert.Equal(5, settings.GetEffectiveSectors().Count);
    }

    [Theory]
    [InlineData("Kepler Reach")]
    [InlineData("My Sector")]
    public void NormalizeAndValidate_RejectsDuplicateSectorNames(string customSector)
    {
        var settings = new StarshipExpeditionSettings { CustomSectors = [customSector, customSector.ToUpperInvariant()] };

        Assert.Throws<ArgumentException>(() => StarshipExpeditionSettings.NormalizeAndValidate(settings));
    }

    [Fact]
    public void NormalizeAndValidate_RejectsBlankOrOversizedSectorNames()
    {
        Assert.Throws<ArgumentException>(() => StarshipExpeditionSettings.NormalizeAndValidate(
            new StarshipExpeditionSettings { CustomSectors = ["  "] }));
        Assert.Throws<ArgumentException>(() => StarshipExpeditionSettings.NormalizeAndValidate(
            new StarshipExpeditionSettings { CustomSectors = [new string('x', StarshipExpeditionSettings.MaximumSectorNameLength + 1)] }));
    }

    [Fact]
    public void NormalizeAndValidate_RejectsInvalidRewardOddsAndUnboundedRanges()
    {
        var wrongOdds = new StarshipExpeditionSettings
        {
            RewardProfiles = [new StarshipRewardProfile { EventType = "SignalDetected", Enabled = true, PointTypeId = 1, CommonWeightPercent = 50 }]
        };
        Assert.Throws<ArgumentException>(() => StarshipExpeditionSettings.NormalizeAndValidate(wrongOdds));

        var unbounded = new StarshipExpeditionSettings
        {
            RewardProfiles = [new StarshipRewardProfile
            {
                EventType = "SignalDetected",
                Enabled = true,
                PointTypeId = 1,
                CommonMinimum = StarshipExpeditionSettings.MaximumConfiguredReward + 1,
                CommonMaximum = StarshipExpeditionSettings.MaximumConfiguredReward + 1
            }]
        };
        Assert.Throws<ArgumentOutOfRangeException>(() => StarshipExpeditionSettings.NormalizeAndValidate(unbounded));
    }

    [Fact]
    public async Task SaveAndLoad_PreservesReusableProfilesRelativeWeightsAndActionTiming()
    {
        var settings = new StarshipExpeditionSettings();
        settings.RewardProfiles.Add(new StarshipRewardProfile
        {
            Id = "shared", Name = "Shared recovery", Enabled = true,
            Tiers =
            [
                new() { Id = "ordinary", Name = "Ordinary", Weight = 3, Rewards = [new() { PointTypeId = 2, Minimum = 1, Maximum = 5 }] },
                new() { Id = "legendary", Name = "Legendary", Weight = 1, Color = "#ffcc00", Rewards = [new() { PointTypeId = 3, Minimum = 10, Maximum = 15 }], Actions = [new() { ActionId = 42, Timing = StarshipActionTimings.AfterBank }] }
            ]
        });
        settings.EventRewardProfileIds["SalvageSite"] = "shared";
        settings.EventRewardProfileIds["SignalDetected"] = "shared";
        var rows = new List<Setting>();

        await CreateStore(rows).SaveAsync(settings);
        var loaded = await CreateStore(rows).LoadAsync();

        var profile = loaded.GetRewardProfile("SalvageSite")!;
        Assert.Same(profile, loaded.GetRewardProfile("SignalDetected"));
        Assert.Equal([75, 25], StarshipRewardTiers.DescribeTiers(profile).Select(tier => tier.ChancePercent));
        Assert.Equal(StarshipActionTimings.AfterBank, profile.Tiers[1].Actions.Single().Timing);
        Assert.Equal(2, profile.Caps.Count);
    }

    [Fact]
    public void NormalizeAndValidate_RejectsMissingProfileBindingsAndDuplicateTierNames()
    {
        var settings = new StarshipExpeditionSettings();
        settings.EventRewardProfileIds["SalvageSite"] = "missing";
        Assert.Throws<ArgumentException>(() => StarshipExpeditionSettings.NormalizeAndValidate(settings));
        settings.EventRewardProfileIds.Clear();
        settings.RewardProfiles[0].Tiers = [new() { Name = "Common" }, new() { Name = "common" }];
        Assert.Throws<ArgumentException>(() => StarshipExpeditionSettings.NormalizeAndValidate(settings));
    }

    private static StarshipExpeditionSettingsStore CreateStore(List<Setting> rows)
    {
        var repository = new Mock<ISettingsRepository>();
        repository.Setup(store => store.GetAsync(
                It.IsAny<Expression<Func<Setting, bool>>>(),
                It.IsAny<Func<IQueryable<Setting>, IOrderedQueryable<Setting>>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string>()))
            .ReturnsAsync(() => rows.ToList());
        repository.Setup(store => store.AddAsync(It.IsAny<Setting>()))
            .Callback<Setting>(setting => rows.Add(setting))
            .Returns((Setting _) => default(ValueTask<EntityEntry<Setting>>));
        repository.Setup(store => store.Update(It.IsAny<Setting>()))
            .Callback<Setting>(setting =>
            {
                var index = rows.FindIndex(existing => existing.Name == setting.Name);
                if (index >= 0) rows[index] = setting;
            });

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(work => work.Settings).Returns(repository.Object);
        unitOfWork.Setup(work => work.SaveChangesAsync()).ReturnsAsync(1);

        var provider = new Mock<IServiceProvider>();
        provider.Setup(serviceProvider => serviceProvider.GetService(typeof(IUnitOfWork))).Returns(unitOfWork.Object);

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(serviceScope => serviceScope.ServiceProvider).Returns(provider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(factory => factory.CreateScope()).Returns(scope.Object);

        return new StarshipExpeditionSettingsStore(scopeFactory.Object);
    }
}
