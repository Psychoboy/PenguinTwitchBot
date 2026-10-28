using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PenguinTwitchBot.Database.Bot.Core.Database;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <summary>
    /// Data-only migration. Runs after <c>MigrateAllIdsToUuidV7</c> has finished all SQLite table rebuilds,
    /// so the raw SQL never executes while a table rebuild is pending.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261027235900_ConvertLegacyIdsToUuidV7Data")]
    public partial class ConvertLegacyIdsToUuidV7Data : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Parent and child keys are rewritten in separate statements; defer FK checks to commit.
            migrationBuilder.Sql("PRAGMA defer_foreign_keys = ON;");
            MigrateAllIdsToUuidV7Helper.ExecutePostMigration(migrationBuilder);
            // Also cover any Guid key column the hardcoded lists miss; the UPDATEs are idempotent.
            MigrateAllIdsToUuidV7Helper.ExecutePostMigration(migrationBuilder, new MigrateAllIdsToUuidV7().TargetModel);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

