using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <summary>
    /// Standalone SQLite migration that disables foreign key enforcement outside a transaction.
    /// This is used to keep the schema-rebuild migration isolated from the pragma boundary.
    /// </summary>
    [DbContext(typeof(PenguinTwitchBot.Database.Bot.Core.Database.ApplicationDbContext))]
    [Migration("20260704160345_DisableSqliteForeignKeysForFishingTournamentRewardCleanup")]
    public partial class DisableSqliteForeignKeysForFishingTournamentRewardCleanup : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys = 1;", suppressTransaction: true);
        }
    }
}