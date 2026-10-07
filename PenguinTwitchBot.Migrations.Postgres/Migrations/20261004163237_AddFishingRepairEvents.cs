using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Username = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ShopItemId = table.Column<int>(type: "integer", nullable: false),
                    UserBoostId = table.Column<int>(type: "integer", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    EquipmentSlot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DurabilityRestored = table.Column<double>(type: "double precision", nullable: false),
                    MaxDurability = table.Column<double>(type: "double precision", nullable: false),
                    DurabilityBefore = table.Column<double>(type: "double precision", nullable: false),
                    DurabilityAfter = table.Column<double>(type: "double precision", nullable: false),
                    GoldPaid = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RepairCostMultiplier = table.Column<double>(type: "double precision", nullable: false),
                    RepairType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RepairedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
