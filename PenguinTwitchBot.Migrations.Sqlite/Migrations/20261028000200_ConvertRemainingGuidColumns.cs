using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PenguinTwitchBot.Database.Bot.Core.Database;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <summary>
    /// Converts legacy integer values in non-key Guid columns (e.g. OBSConnectionId, TournamentId, ShopItemId
    /// on sub-actions) that the earlier key-only conversion skipped. Idempotent.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261028000200_ConvertRemainingGuidColumns")]
    public partial class ConvertRemainingGuidColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA defer_foreign_keys = ON;");
            MigrateAllIdsToUuidV7Helper.ExecutePostMigration(migrationBuilder, new MigrateAllIdsToUuidV7().TargetModel);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
