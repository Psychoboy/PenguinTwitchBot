using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExpeditionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false),
                    StartedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReturnReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TotalLegs = table.Column<int>(type: "integer", nullable: false),
                    CompletedLegs = table.Column<int>(type: "integer", nullable: false),
                    CrewCount = table.Column<int>(type: "integer", nullable: false),
                    EventsSucceeded = table.Column<int>(type: "integer", nullable: false),
                    EventsFailed = table.Column<int>(type: "integer", nullable: false),
                    TotalRewardsPaid = table.Column<long>(type: "bigint", nullable: false),
                    HullRemaining = table.Column<int>(type: "integer", nullable: true),
                    HullMaximum = table.Column<int>(type: "integer", nullable: true),
                    SuppliesRemaining = table.Column<int>(type: "integer", nullable: true),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false)
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
