using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddFishingTournaments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FishingTournaments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    PrimaryScoreCategory = table.Column<int>(type: "INTEGER", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndsAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AutoScheduleEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoScheduleCron = table.Column<string>(type: "TEXT", nullable: false),
                    RunDurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    EntryFeeAmount = table.Column<int>(type: "INTEGER", nullable: true),
                    EntryFeePointTypeId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FishingTournaments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FishingTournaments_PointTypes_EntryFeePointTypeId",
                        column: x => x.EntryFeePointTypeId,
                        principalTable: "PointTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FishingTournamentFishTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FishingTournamentId = table.Column<int>(type: "INTEGER", nullable: false),
                    FishTypeId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FishingTournamentFishTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FishingTournamentFishTypes_FishTypes_FishTypeId",
                        column: x => x.FishTypeId,
                        principalTable: "FishTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FishingTournamentFishTypes_FishingTournaments_FishingTournamentId",
                        column: x => x.FishingTournamentId,
                        principalTable: "FishingTournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FishingTournamentRewardRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FishingTournamentId = table.Column<int>(type: "INTEGER", nullable: false),
                    ScoreCategory = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetFishTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    MinPlacement = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxPlacement = table.Column<int>(type: "INTEGER", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    PointTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FishingTournamentRewardRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FishingTournamentRewardRules_FishTypes_TargetFishTypeId",
                        column: x => x.TargetFishTypeId,
                        principalTable: "FishTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FishingTournamentRewardRules_FishingTournaments_FishingTournamentId",
                        column: x => x.FishingTournamentId,
                        principalTable: "FishingTournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FishingTournamentRewardRules_PointTypes_PointTypeId",
                        column: x => x.PointTypeId,
                        principalTable: "PointTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FishingTournamentFishTypes_FishTypeId",
                table: "FishingTournamentFishTypes",
                column: "FishTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_FishingTournamentFishTypes_Tournament_Fish",
                table: "FishingTournamentFishTypes",
                columns: new[] { "FishingTournamentId", "FishTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FishingTournamentRewardRules_PointTypeId",
                table: "FishingTournamentRewardRules",
                column: "PointTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_FishingTournamentRewardRules_TargetFishTypeId",
                table: "FishingTournamentRewardRules",
                column: "TargetFishTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_FishingTournamentRewardRules_Tournament_Category_Bracket",
                table: "FishingTournamentRewardRules",
                columns: new[] { "FishingTournamentId", "ScoreCategory", "TargetFishTypeId", "MinPlacement", "MaxPlacement" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FishingTournaments_Enabled_Status_StartsAtUtc",
                table: "FishingTournaments",
                columns: new[] { "Enabled", "Status", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FishingTournaments_EntryFeePointTypeId",
                table: "FishingTournaments",
                column: "EntryFeePointTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FishingTournamentFishTypes");

            migrationBuilder.DropTable(
                name: "FishingTournamentRewardRules");

            migrationBuilder.DropTable(
                name: "FishingTournaments");
        }
    }
}
