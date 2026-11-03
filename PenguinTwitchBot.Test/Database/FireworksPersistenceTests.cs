using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Core.Database;

namespace PenguinTwitchBot.Test.Database
{
    public class FireworksPersistenceTests
    {
        [Fact]
        public async Task FireworksSubAction_RoundTripsThroughItsDedicatedTable()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var context = new ApplicationDbContext(options);
            await context.Database.EnsureCreatedAsync();

            var fireworks = new FireworksType
            {
                Count = "$math(%Amount% + 1)$",
                StartDelayMilliseconds = "%StartDelay%",
                DelayMilliseconds = "%Delay%",
                DelayVarianceMilliseconds = "%Variance%",
                Shell = "Ring",
                Size = "8"
            };
            context.Actions.Add(new ActionType
            {
                Name = "Fireworks Test",
                SubActions = [fireworks]
            });
            await context.SaveChangesAsync();

            var loaded = await context.Actions
                .Include(action => action.SubActions)
                .SingleAsync(action => action.Name == "Fireworks Test");

            var loadedFireworks = Assert.IsType<FireworksType>(Assert.Single(loaded.SubActions));
            Assert.Equal("$math(%Amount% + 1)$", loadedFireworks.Count);
            Assert.Equal("%StartDelay%", loadedFireworks.StartDelayMilliseconds);
            Assert.Equal("%Delay%", loadedFireworks.DelayMilliseconds);
            Assert.Equal("%Variance%", loadedFireworks.DelayVarianceMilliseconds);
            Assert.Equal("Ring", loadedFireworks.Shell);
            Assert.Equal("8", loadedFireworks.Size);
            Assert.Equal("subactions_fireworks", context.Model.FindEntityType(typeof(FireworksType))!.GetTableName());
        }
    }
}