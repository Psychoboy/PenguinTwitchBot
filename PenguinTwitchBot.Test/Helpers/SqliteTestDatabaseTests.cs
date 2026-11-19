using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Credits;

namespace PenguinTwitchBot.Test.Helpers
{
    public class SqliteTestDatabaseTests
    {
        [Fact]
        public async Task CreateConnection_ReturnsIsolatedInMemoryDatabases()
        {
            await using var connA = SqliteTestDatabase.CreateConnection();
            await using var connB = SqliteTestDatabase.CreateConnection();

            var optsA = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connA).Options;
            var optsB = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connB).Options;

            await using (var ctxA = new ApplicationDbContext(optsA))
            {
                ctxA.CreditThemes.Add(new CreditTheme { Name = "Isolation Check", ThemeJson = "{}" });
                await ctxA.SaveChangesAsync();
            }

            await using var ctxB = new ApplicationDbContext(optsB);
            Assert.Empty(await ctxB.CreditThemes.ToListAsync());
        }

        [Fact]
        public async Task CreateConnection_EnforcesForeignKeysPragma()
        {
            await using var conn = SqliteTestDatabase.CreateConnection();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA foreign_keys;";
            var result = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(1, result);
        }

        [Fact]
        public async Task CreateMigratedConnection_IncludesAppliedMigrationsHistory()
        {
            await using var conn = SqliteTestDatabase.CreateMigratedConnection();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\";";
            var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            Assert.True(count > 0);
        }
    }
}
