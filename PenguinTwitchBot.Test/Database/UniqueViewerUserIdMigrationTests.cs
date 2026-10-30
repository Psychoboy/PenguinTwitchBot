using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PenguinTwitchBot.Database.Bot.Core.Database;
using Xunit;

namespace PenguinTwitchBot.Test.Database
{
    public class UniqueViewerUserIdMigrationTests
    {
        private const string PreviousMigration = "20261029194818_AddOverlayTimerVisibility";

        [Fact]
        public void Migration_RemovesDuplicateViewers_KeepsLatestAndTitle_ThenEnforcesUniqueness()
        {
            var path = Path.Combine(Path.GetTempPath(), $"viewers_{Guid.NewGuid():N}.db");
            var cs = $"Data Source={path}";
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(cs, o => o.MigrationsAssembly("PenguinTwitchBot.Migrations.Sqlite"))
                .Options;

            using (var ctx = new ApplicationDbContext(options))
            {
                ctx.GetService<IMigrator>().Migrate(PreviousMigration);
            }

            using (var conn = new SqliteConnection(cs))
            {
                conn.Open();
                Exec(conn, """
                    INSERT INTO Viewers (Id, UserId, Username, DisplayName, Title, LastSeen, isSub, isVip, isMod, isEditor, isBroadcaster) VALUES
                    ('00000000-0000-7000-8000-00000000a3cc','73229846','riffderplays','RiffderPlays','Boss','2026-10-29 16:25:22',0,0,0,0,0),
                    ('00000000-0000-7000-8000-00000000a3cd','73229846','riffderplays','RiffderPlays','','2026-10-29 22:41:01',0,0,0,0,0),
                    ('00000000-0000-7000-8000-00000000a3ce','111','solo','Solo','','2026-10-01 00:00:00',0,0,0,0,0),
                    ('00000000-0000-7000-8000-00000000a3cf','','blank1','Blank1','','2026-10-01 00:00:00',0,0,0,0,0),
                    ('00000000-0000-7000-8000-00000000a3d0','','blank2','Blank2','','2026-10-02 00:00:00',0,0,0,0,0);
                    """);
            }

            using (var ctx = new ApplicationDbContext(options))
            {
                ctx.GetService<IMigrator>().Migrate();
            }

            using var verify = new SqliteConnection(cs);
            verify.Open();

            var kept = Query(verify, "SELECT Id, Title FROM Viewers WHERE UserId = '73229846'");
            var row = Assert.Single(kept);
            Assert.Equal("00000000-0000-7000-8000-00000000a3cd|Boss", row);
            Assert.Equal("4", Query(verify, "SELECT COUNT(*) FROM Viewers").Single());

            var ex = Assert.Throws<SqliteException>(() => Exec(verify,
                "INSERT INTO Viewers (Id, UserId, Username, DisplayName, Title, LastSeen, isSub, isVip, isMod, isEditor, isBroadcaster) VALUES ('x','111','dup','Dup','','2026-10-30',0,0,0,0,0)"));
            Assert.Equal(19, ex.SqliteErrorCode);

            Exec(verify, "INSERT INTO Viewers (Id, UserId, Username, DisplayName, Title, LastSeen, isSub, isVip, isMod, isEditor, isBroadcaster) VALUES ('y','','blank3','Blank3','','2026-10-30',0,0,0,0,0)");
        }

        private static void Exec(SqliteConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        private static List<string> Query(SqliteConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            using var r = cmd.ExecuteReader();
            var rows = new List<string>();
            while (r.Read())
            {
                rows.Add(string.Join("|", Enumerable.Range(0, r.FieldCount).Select(i => r.GetValue(i).ToString())));
            }
            return rows;
        }
    }
}
