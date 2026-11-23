using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Services;

namespace PenguinTwitchBot.Test.Services;

public class RaidHistorySettingsServiceTests
{
    private static (RaidHistorySettingsService Sut, List<Setting> Store, IUnitOfWork Db) CreateSut()
    {
        var store = new List<Setting>();
        var db = Substitute.For<IUnitOfWork>();
        var settingsRepo = Substitute.For<ISettingsRepository>();
        db.Settings.Returns(settingsRepo);

        settingsRepo.GetAsync(Arg.Any<Expression<Func<Setting, bool>>>())
            .Returns(callInfo =>
            {
                var predicate = callInfo.Arg<Expression<Func<Setting, bool>>>().Compile();
                return Task.FromResult(store.Where(predicate).ToList());
            });

        settingsRepo.AddAsync(Arg.Any<Setting>())
            .Returns(callInfo =>
            {
                store.Add(callInfo.Arg<Setting>());
                return ValueTask.FromResult<EntityEntry<Setting>>(null!);
            });

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(db);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        return (new RaidHistorySettingsService(scopeFactory), store, db);
    }

    [Fact]
    public async Task GetShowLiveTeamMembersAsync_WhenNotSet_DefaultsToTrue()
    {
        var (sut, _, _) = CreateSut();

        var enabled = await sut.GetShowLiveTeamMembersAsync();

        Assert.True(enabled);
    }

    [Fact]
    public async Task SetShowLiveTeamMembersAsync_PersistsFalseAndTrue()
    {
        var (sut, store, db) = CreateSut();

        await sut.SetShowLiveTeamMembersAsync(false);
        Assert.False(await sut.GetShowLiveTeamMembersAsync());
        Assert.Single(store);
        Assert.Equal(0, store[0].IntSetting);

        await sut.SetShowLiveTeamMembersAsync(true);
        Assert.True(await sut.GetShowLiveTeamMembersAsync());
        Assert.Single(store);
        Assert.Equal(1, store[0].IntSetting);
        await db.Received(2).SaveChangesAsync();
    }
}
