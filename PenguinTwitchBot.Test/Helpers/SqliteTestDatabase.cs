using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Migrations.Sqlite;

namespace PenguinTwitchBot.Test.Helpers
{
    /// <summary>
    /// Builds each EF Core SQLite schema once per test process (either via EnsureCreated or full migrations)
    /// and clones it into isolated in-memory connections via SQLite's online backup API.
    /// </summary>
    internal static class SqliteTestDatabase
    {
        private const string InMemoryConnectionString = "Data Source=:memory:;Foreign Keys=True";

        private static readonly object EnsureCreatedLock = new();
        private static readonly object MigratedLock = new();

        private static readonly Lazy<SqliteConnection> EnsureCreatedTemplate = new(
            BuildEnsureCreatedTemplate,
            LazyThreadSafetyMode.ExecutionAndPublication);

        private static readonly Lazy<SqliteConnection> MigratedTemplate = new(
            BuildMigratedTemplate,
            LazyThreadSafetyMode.ExecutionAndPublication);

        public static SqliteConnection CreateConnection()
        {
            var template = EnsureCreatedTemplate.Value;
            var connection = new SqliteConnection(InMemoryConnectionString);
            connection.Open();
            lock (EnsureCreatedLock)
            {
                template.BackupDatabase(connection);
            }
            return connection;
        }

        public static SqliteConnection CreateMigratedConnection()
        {
            var template = MigratedTemplate.Value;
            var connection = new SqliteConnection(InMemoryConnectionString);
            connection.Open();
            lock (MigratedLock)
            {
                template.BackupDatabase(connection);
            }
            return connection;
        }

        private static SqliteConnection BuildEnsureCreatedTemplate()
        {
            var connection = new SqliteConnection(InMemoryConnectionString);
            connection.Open();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            using var context = new ApplicationDbContext(options);
            context.Database.EnsureCreated();
            return connection;
        }

        private static SqliteConnection BuildMigratedTemplate()
        {
            var connection = new SqliteConnection(InMemoryConnectionString);
            connection.Open();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection, sqlite => sqlite.MigrationsAssembly(
                    typeof(SqliteDesignTimeDbContextFactory).Assembly.GetName().Name))
                .Options;
            using var context = new ApplicationDbContext(options);
            context.Database.Migrate();
            return connection;
        }
    }
}
