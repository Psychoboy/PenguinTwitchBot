using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddFishingRepairEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FishingRepairEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ShopItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserBoostId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    EquipmentSlot = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    DurabilityRestored = table.Column<double>(type: "REAL", nullable: false),
                    MaxDurability = table.Column<double>(type: "REAL", nullable: false),
                    DurabilityBefore = table.Column<double>(type: "REAL", nullable: false),
                    DurabilityAfter = table.Column<double>(type: "REAL", nullable: false),
                    GoldPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RepairCostMultiplier = table.Column<double>(type: "REAL", nullable: false),
                    RepairType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    RepairedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FishingRepairEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FishingRepairEvents_EquipmentSlot_RepairedAt",
                table: "FishingRepairEvents",
                columns: new[] { "EquipmentSlot", "RepairedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FishingRepairEvents_RepairedAt",
                table: "FishingRepairEvents",
                column: "RepairedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FishingRepairEvents_UserId_RepairedAt",
                table: "FishingRepairEvents",
                columns: new[] { "UserId", "RepairedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FishingRepairEvents");
        }
    }
}
