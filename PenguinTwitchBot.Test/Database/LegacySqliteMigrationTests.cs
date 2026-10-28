using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PenguinTwitchBot.Database.Bot.Core.Database;
using Xunit;

namespace PenguinTwitchBot.Test.Database
{
    public class LegacySqliteMigrationTests
    {
        [Fact]
        public void LegacyIntIds_MigrateToDeterministicUuids_WithForeignKeysIntact()
        {
            var path = Path.Combine(Path.GetTempPath(), $"legacy_{Guid.NewGuid():N}.db");
            var cs = $"Data Source={path};Foreign Keys=True";
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(cs, o => o.MigrationsAssembly("PenguinTwitchBot.Migrations.Sqlite"))
                .Options;

            using (var ctx = new ApplicationDbContext(options))
            {
                ctx.GetService<IMigrator>().Migrate("20261027163815_RefactorMinigameSubActions");
            }

            using (var seed = new SqliteConnection($"Data Source={path};Foreign Keys=False"))
            {
                seed.Open();
                using var seedCmd = seed.CreateCommand();
                seedCmd.CommandText = "INSERT INTO Playlists (Id, Name) VALUES (1,'a'),(11,'b'); INSERT INTO Songs (Id, Duration, MusicPlaylistId, RequestedBy, SongId, Title) VALUES (1,'00:03:00',1,'x','s1','t1'),(12,'00:03:00',11,'x','s2','t2'),(13,'00:03:00',11,'x','s3','t3'),(4,'00:03:00',99,'x','s4','orphan');";
                seedCmd.ExecuteNonQuery();
            }

            using (var ctx = new ApplicationDbContext(options))
            {
                var mig = ctx.GetService<IMigrator>();
                try { mig.Migrate("20261027233427_MigrateAllIdsToUuidV7"); }
                catch (Exception e) { throw new Exception("SCHEMA MIGRATION FAILED: " + e.Message, e); }
                try { mig.Migrate(); }
                catch (Exception e) { throw new Exception("DATA MIGRATION FAILED: " + e.Message, e); }
            }

            using var conn = new SqliteConnection(cs);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT s.Id, s.MusicPlaylistId, p.Id FROM Songs s JOIN Playlists p ON p.Id = s.MusicPlaylistId ORDER BY s.Id";
            using var r = cmd.ExecuteReader();
            var rows = new List<string>();
            while (r.Read()) rows.Add($"{r.GetString(0)}|{r.GetString(1)}|{r.GetString(2)}");
            Assert.True(rows.Count >= 3);
            Assert.All(rows, x => Assert.StartsWith("00000000-0000-7000-8000-", x));

            // Guid lookups and UPDATEs through EF must hit rows whose legacy id needed hex letters (11 -> ...0B).
            using var verify = new ApplicationDbContext(options);
            var playlistId = PenguinTwitchBot.Database.Bot.DatabaseTools.UuidMigrationHelper.FromLegacyInt(11);
            var playlist = verify.Playlists.Single(p => p.Id == playlistId);
            Assert.Equal(2, playlist.Songs.Count);
            playlist.Name = "renamed";
            Assert.Equal(1, verify.SaveChanges());
        }
    }
}
