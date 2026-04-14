using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class addEquipmentSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEquipped",
                table: "UserFishingBoosts",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUsedAt",
                table: "UserFishingBoosts",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemainingUses",
                table: "UserFishingBoosts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EquipmentSlot",
                table: "FishingShopItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsConsumable",
                table: "FishingShopItems",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxUses",
                table: "FishingShopItems",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsEquipped",
                table: "UserFishingBoosts");

            migrationBuilder.DropColumn(
                name: "LastUsedAt",
                table: "UserFishingBoosts");

            migrationBuilder.DropColumn(
                name: "RemainingUses",
                table: "UserFishingBoosts");

            migrationBuilder.DropColumn(
                name: "EquipmentSlot",
                table: "FishingShopItems");

            migrationBuilder.DropColumn(
                name: "IsConsumable",
                table: "FishingShopItems");

            migrationBuilder.DropColumn(
                name: "MaxUses",
                table: "FishingShopItems");
        }
    }
}
