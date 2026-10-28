using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PenguinTwitchBot.Bot.Commands.Games.UfoClaw;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Overlay;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Migrations.Sqlite;

namespace PenguinTwitchBot.Test.Database;

public class UfoClawPersistenceTests
{
    [Fact]
    public async Task MigratedSqliteRoundTripsClawControlSettingsAndFrozenGrab()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var services = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection,
                sqlite => sqlite.MigrationsAssembly(typeof(SqliteDesignTimeDbContextFactory).Assembly.GetName().Name)))
            .AddScoped<IUnitOfWork, UnitOfWork>().BuildServiceProvider();
        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.MigrateAsync();
            context.Add(new UfoClawControlType { Id = Guid.CreateVersion7(), Operation = "show", PrizeSetId = "set" });
            await context.SaveChangesAsync();
        }
        var store = new UfoClawStore(services.GetRequiredService<IServiceScopeFactory>());
        await store.SaveAsync(new()
        {
            Mode = "prizes", PrizeSetId = "set", Visible = true, Revision = 5,
            Settings = new() { LeftSafeGapPixels = 240, RightSafeGapPixels = 320, PitBackgroundEnabled = false, SlipChancePercent = 35, MaxSuspenseDrops = 5,
                PrizeSets = [new() { Id = "set", Name = "Prizes", Entries = [new() { Id = "one", Label = "Gift", Weight = 0.25m }] }] },
            Grab = new() { StartedAtUtc = DateTimeOffset.UtcNow, Entries = [new() { Id = "one", Label = "Gift" }] }
        });
        var restored = await new UfoClawStore(services.GetRequiredService<IServiceScopeFactory>()).LoadAsync();
        Assert.Equal(5, restored.Revision);
        Assert.Equal(240, restored.Settings.LeftSafeGapPixels);
        Assert.Equal(320, restored.Settings.RightSafeGapPixels);
        Assert.False(restored.Settings.PitBackgroundEnabled);
        Assert.Equal(35, restored.Settings.SlipChancePercent);
        Assert.Equal(5, restored.Settings.MaxSuspenseDrops);
        Assert.Equal(0.25m, restored.Settings.PrizeSets[0].Entries[0].Weight);
        Assert.Equal("Gift", restored.Grab!.Winner.Label);
        await using var verification = services.CreateAsyncScope();
        var loaded = await verification.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<UfoClawControlType>().SingleAsync();
        Assert.Equal("show", loaded.Operation);
        Assert.Equal("set", loaded.PrizeSetId);
    }

    [Fact]
    public void WidgetUsesFullScreenProductionSource()
    {
        var widget = WidgetRegistry.Find("ufoclaw");
        Assert.NotNull(widget);
        Assert.Equal(1920, widget.DefaultWidth);
        Assert.Equal(1080, widget.DefaultHeight);
        Assert.Equal("/ufo.html", widget.SourcePath);
    }
}