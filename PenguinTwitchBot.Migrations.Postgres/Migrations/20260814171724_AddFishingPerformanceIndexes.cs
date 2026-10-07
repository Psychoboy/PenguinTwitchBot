using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddFishingPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CONCURRENTLY avoids locking these tables for writes while the index builds;
            // each statement must run outside the migration's ambient transaction.
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_UserFishingBoosts_UserId_IsEquipped\" ON \"UserFishingBoosts\" (\"UserId\", \"IsEquipped\");",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_FishingGolds_UserId\" ON \"FishingGolds\" (\"UserId\");",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_FishCatches_CaughtAt\" ON \"FishCatches\" (\"CaughtAt\");",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_FishCatches_GoldEarned\" ON \"FishCatches\" (\"GoldEarned\");",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_FishCatches_UserId_FishTypeId\" ON \"FishCatches\" (\"UserId\", \"FishTypeId\");",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS \"IX_UserFishingBoosts_UserId_IsEquipped\";", suppressTransaction: true);
            migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS \"IX_FishingGolds_UserId\";", suppressTransaction: true);
            migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS \"IX_FishCatches_CaughtAt\";", suppressTransaction: true);
            migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS \"IX_FishCatches_GoldEarned\";", suppressTransaction: true);
            migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS \"IX_FishCatches_UserId_FishTypeId\";", suppressTransaction: true);
        }
    }
}
