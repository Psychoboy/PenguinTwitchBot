using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddFishingFailureChancesDurabilityAndRepair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CurrentDurability",
                table: "UserFishingBoosts",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DisableBreaking",
                table: "FishingShopItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "DurabilityLossPerUse",
                table: "FishingShopItems",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "MaxDurability",
                table: "FishingShopItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "NetBreakChance",
                table: "FishingSettings",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "ReelJamChance",
                table: "FishingSettings",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RepairCostMultiplier",
                table: "FishingSettings",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "TackleBoxLostChance",
                table: "FishingSettings",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentDurability",
                table: "UserFishingBoosts");

            migrationBuilder.DropColumn(
                name: "DisableBreaking",
                table: "FishingShopItems");

            migrationBuilder.DropColumn(
                name: "DurabilityLossPerUse",
                table: "FishingShopItems");

            migrationBuilder.DropColumn(
                name: "MaxDurability",
                table: "FishingShopItems");

            migrationBuilder.DropColumn(
                name: "NetBreakChance",
                table: "FishingSettings");

            migrationBuilder.DropColumn(
                name: "ReelJamChance",
                table: "FishingSettings");

            migrationBuilder.DropColumn(
                name: "RepairCostMultiplier",
                table: "FishingSettings");

            migrationBuilder.DropColumn(
                name: "TackleBoxLostChance",
                table: "FishingSettings");
        }
    }
}
