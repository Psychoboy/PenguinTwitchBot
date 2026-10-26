using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddStarshipExpeditionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "starship_expedition_history",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExpeditionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShipName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Seed = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StartedByUserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ReturnReason = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TotalLegs = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedLegs = table.Column<int>(type: "INTEGER", nullable: false),
                    CrewCount = table.Column<int>(type: "INTEGER", nullable: false),
                    EventsSucceeded = table.Column<int>(type: "INTEGER", nullable: false),
                    EventsFailed = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalRewardsPaid = table.Column<long>(type: "INTEGER", nullable: false),
                    HullRemaining = table.Column<int>(type: "INTEGER", nullable: true),
                    HullMaximum = table.Column<int>(type: "INTEGER", nullable: true),
                    SuppliesRemaining = table.Column<int>(type: "INTEGER", nullable: true),
                    SnapshotJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_starship_expedition_history", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_starship_expedition_history_EndedAtUtc",
                table: "starship_expedition_history",
                column: "EndedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_starship_expedition_history_ExpeditionId",
                table: "starship_expedition_history",
                column: "ExpeditionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_starship_expedition_history_StartedAtUtc",
                table: "starship_expedition_history",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_starship_expedition_history_Status",
                table: "starship_expedition_history",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "starship_expedition_history");
        }
    }
}
