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
