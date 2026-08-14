using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddFishingPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserFishingBoosts_UserId_IsEquipped",
                table: "UserFishingBoosts",
                columns: new[] { "UserId", "IsEquipped" });

            migrationBuilder.CreateIndex(
                name: "IX_FishingGolds_UserId",
                table: "FishingGolds",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FishCatches_CaughtAt",
                table: "FishCatches",
                column: "CaughtAt");

            migrationBuilder.CreateIndex(
                name: "IX_FishCatches_GoldEarned",
                table: "FishCatches",
                column: "GoldEarned");

            migrationBuilder.CreateIndex(
                name: "IX_FishCatches_UserId_FishTypeId",
                table: "FishCatches",
                columns: new[] { "UserId", "FishTypeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserFishingBoosts_UserId_IsEquipped",
                table: "UserFishingBoosts");

            migrationBuilder.DropIndex(
                name: "IX_FishingGolds_UserId",
                table: "FishingGolds");

            migrationBuilder.DropIndex(
                name: "IX_FishCatches_CaughtAt",
                table: "FishCatches");

            migrationBuilder.DropIndex(
                name: "IX_FishCatches_GoldEarned",
                table: "FishCatches");

            migrationBuilder.DropIndex(
                name: "IX_FishCatches_UserId_FishTypeId",
                table: "FishCatches");
        }
    }
}
