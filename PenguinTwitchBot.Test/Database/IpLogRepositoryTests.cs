using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.IpLogs;
using PenguinTwitchBot.Database.Repository.Repositories;
using Xunit;

namespace PenguinTwitchBot.Test.Database
{
    public class IpLogRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly IpLogRepository _repository;

        public IpLogRepositoryTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();
            _repository = new IpLogRepository(_context);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Close();
            _connection.Dispose();
        }

        [Fact]
        public async Task GetKnownIpsForUser_ReturnsEntries_ByUserId()
        {
            // Arrange
            _context.IpLogEntrys.AddRange(
                new IpLogEntry { Id = "1", UserId = "user-100", Username = "oldname", Ip = "10.0.0.1", Count = 1 },
                new IpLogEntry { Id = "2", UserId = "user-100", Username = "newname", Ip = "10.0.0.2", Count = 2 },
                new IpLogEntry { Id = "3", UserId = "user-200", Username = "otheruser", Ip = "10.0.0.3", Count = 1 }
            );
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetKnownIpsForUser("newname", "user-100");

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Contains(result, x => x.Ip == "10.0.0.1");
            Assert.Contains(result, x => x.Ip == "10.0.0.2");
        }

        [Fact]
        public async Task GetKnownIpsForUser_FallsBackToUsername_WhenUserIdIsNull()
        {
            // Arrange
            _context.IpLogEntrys.AddRange(
                new IpLogEntry { Id = "1", UserId = "user-100", Username = "myuser", Ip = "10.0.0.1", Count = 1 },
                new IpLogEntry { Id = "2", UserId = "user-200", Username = "otheruser", Ip = "10.0.0.2", Count = 1 }
            );
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetKnownIpsForUser("myuser", null);

            // Assert
            Assert.Single(result);
            Assert.Equal("10.0.0.1", result[0].Ip);
        }

        [Fact]
        public async Task GetDuplicateIpsForUser_DoesNotMatchSameUser_WhenUserHasDifferentUsernames()
        {
            // Arrange: Same user "user-100" previously logged as "oldname", now as "newname" from same IP
            _context.IpLogEntrys.AddRange(
                new IpLogEntry { Id = "1", UserId = "user-100", Username = "oldname", Ip = "192.168.1.50", Count = 2 },
                new IpLogEntry { Id = "2", UserId = "user-100", Username = "newname", Ip = "192.168.1.50", Count = 1 }
            );
            await _context.SaveChangesAsync();

            // Act: Search for duplicate IP users for user-100
            var result = await _repository.GetDuplicateIpsForUser("newname", "user-100");

            // Assert: Must NOT return oldname as a duplicate user because it's the SAME userId
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetDuplicateIpsForUser_MatchesDifferentUser_SharingSameIp()
        {
            // Arrange: Two different users share the same IP
            _context.IpLogEntrys.AddRange(
                new IpLogEntry { Id = "1", UserId = "user-100", Username = "alice", Ip = "192.168.1.50", Count = 1 },
                new IpLogEntry { Id = "2", UserId = "user-200", Username = "bob", Ip = "192.168.1.50", Count = 3 },
                new IpLogEntry { Id = "3", UserId = "user-300", Username = "charlie", Ip = "10.0.0.1", Count = 1 }
            );
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetDuplicateIpsForUser("alice", "user-100");

            // Assert: Bob shares the IP with Alice
            Assert.Single(result);
            Assert.Equal("bob", result[0].Username);
            Assert.Equal("user-200", result[0].UserId);
        }

        [Fact]
        public async Task GetAllUsersWithDuplicateIps_ExcludesSelfDuplicates_WhenUserRenamed()
        {
            // Arrange:
            // IP 1.1.1.1 is shared between user-100 and user-200
            // user-100 has two rows with different historical names ("alice_old", "alice_new")
            // user-300 is alone on IP 2.2.2.2 with two historical names
            _context.IpLogEntrys.AddRange(
                new IpLogEntry { Id = "1", UserId = "user-100", Username = "alice_old", Ip = "1.1.1.1", ConnectedDate = DateTime.UtcNow.AddDays(-10) },
                new IpLogEntry { Id = "2", UserId = "user-100", Username = "alice_new", Ip = "1.1.1.1", ConnectedDate = DateTime.UtcNow },
                new IpLogEntry { Id = "3", UserId = "user-200", Username = "bob", Ip = "1.1.1.1", ConnectedDate = DateTime.UtcNow },
                new IpLogEntry { Id = "4", UserId = "user-300", Username = "charlie_old", Ip = "2.2.2.2", ConnectedDate = DateTime.UtcNow.AddDays(-5) },
                new IpLogEntry { Id = "5", UserId = "user-300", Username = "charlie_new", Ip = "2.2.2.2", ConnectedDate = DateTime.UtcNow }
            );
            await _context.SaveChangesAsync();

            // Act
            var pairs = await _repository.GetAllUsersWithDuplicateIps();

            // Assert:
            // There should be exactly ONE pair: (alice_new, bob)
            // charlie should not be paired with itself, and alice should not be paired with herself
            Assert.Single(pairs);
            var pair = pairs[0];
            Assert.True(
                (pair.User1 == "alice_new" && pair.User2 == "bob") ||
                (pair.User1 == "bob" && pair.User2 == "alice_new")
            );
        }

        [Fact]
        public async Task MigrationSql_BackfillsFromViewers_PurgesMissingIds_AndDeduplicates()
        {
            // Arrange
            _context.Viewers.Add(new PenguinTwitchBot.Database.Bot.Models.Viewer
            {
                Username = "knownuser",
                UserId = "twitch-known-123"
            });

            _context.IpLogEntrys.AddRange(
                // 1. Missing UserId, but matching viewer exists -> should be backfilled
                new IpLogEntry { Id = "row1", Username = "knownuser", UserId = "", Ip = "1.1.1.1", Count = 2, ConnectedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                // 2. Missing UserId, no viewer -> should be deleted
                new IpLogEntry { Id = "row2", Username = "unknownuser", UserId = "", Ip = "2.2.2.2", Count = 1, ConnectedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                // 3. Anonymous user -> should be deleted
                new IpLogEntry { Id = "row3", Username = "anonymous", UserId = "", Ip = "3.3.3.3", Count = 1, ConnectedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                // 4 & 5. Duplicate (UserId, Ip) -> should be merged to one row with sum count = 8, latest date = 2026-02-01, latest username = alice_new
                new IpLogEntry { Id = "row4", Username = "alice_old", UserId = "u1", Ip = "4.4.4.4", Count = 3, ConnectedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new IpLogEntry { Id = "row5", Username = "alice_new", UserId = "u1", Ip = "4.4.4.4", Count = 5, ConnectedDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc) },
                // 6. Distinct IP for same user u1 -> should remain
                new IpLogEntry { Id = "row6", Username = "alice_new", UserId = "u1", Ip = "5.5.5.5", Count = 1, ConnectedDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc) }
            );
            await _context.SaveChangesAsync();

            // Act - Execute the exact migration SQL
            await _context.Database.ExecuteSqlRawAsync("""
                UPDATE "IpLogEntrys"
                SET "UserId" = (
                    SELECT v."UserId"
                    FROM "Viewers" v
                    WHERE lower(v."Username") = lower("IpLogEntrys"."Username") AND v."UserId" IS NOT NULL AND v."UserId" != ''
                    LIMIT 1
                )
                WHERE ("UserId" IS NULL OR "UserId" = '')
                  AND EXISTS (
                    SELECT 1
                    FROM "Viewers" v
                    WHERE lower(v."Username") = lower("IpLogEntrys"."Username") AND v."UserId" IS NOT NULL AND v."UserId" != ''
                  );

                DELETE FROM "IpLogEntrys" WHERE "UserId" IS NULL OR trim("UserId") = '' OR lower("Username") = 'anonymous';

                CREATE TEMP TABLE "_IpLogMerged" AS
                SELECT
                    min("Id") AS "Id",
                    "UserId",
                    "Ip",
                    max("ConnectedDate") AS "ConnectedDate",
                    sum("Count") AS "Count",
                    (SELECT i2."Username" FROM "IpLogEntrys" i2 WHERE i2."UserId" = i1."UserId" AND i2."Ip" = i1."Ip" ORDER BY i2."ConnectedDate" DESC, i2."Id" DESC LIMIT 1) AS "Username"
                FROM "IpLogEntrys" i1
                GROUP BY "UserId", "Ip";

                DELETE FROM "IpLogEntrys";

                INSERT INTO "IpLogEntrys" ("Id", "UserId", "Ip", "Username", "Count", "ConnectedDate")
                SELECT "Id", "UserId", "Ip", "Username", "Count", "ConnectedDate" FROM "_IpLogMerged";

                DROP TABLE "_IpLogMerged";
                """);

            _context.ChangeTracker.Clear();

            // Assert
            var remaining = await _context.IpLogEntrys.AsNoTracking().ToListAsync();
            Assert.Equal(3, remaining.Count);

            // Backfilled row
            var backfilled = remaining.FirstOrDefault(x => x.Ip == "1.1.1.1");
            Assert.NotNull(backfilled);
            Assert.Equal("twitch-known-123", backfilled.UserId);
            Assert.Equal("knownuser", backfilled.Username);

            // Merged row for 4.4.4.4
            var merged = remaining.FirstOrDefault(x => x.Ip == "4.4.4.4");
            Assert.NotNull(merged);
            Assert.Equal("u1", merged.UserId);
            Assert.Equal("alice_new", merged.Username);
            Assert.Equal(8, merged.Count); // 3 + 5
            Assert.Equal(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), merged.ConnectedDate);

            // Preserved distinct IP row for 5.5.5.5
            var distinctIpRow = remaining.FirstOrDefault(x => x.Ip == "5.5.5.5");
            Assert.NotNull(distinctIpRow);
            Assert.Equal("u1", distinctIpRow.UserId);
            Assert.Equal(1, distinctIpRow.Count);
        }
    }
}
