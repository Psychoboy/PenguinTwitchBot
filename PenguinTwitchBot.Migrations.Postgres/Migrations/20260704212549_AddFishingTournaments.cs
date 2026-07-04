using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PrimaryScoreCategory = table.Column<int>(type: "integer", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AutoScheduleEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AutoScheduleCron = table.Column<string>(type: "text", nullable: false),
                    RunDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    EntryFeeAmount = table.Column<long>(type: "bigint", nullable: true),
                    EntryFeePointTypeId = table.Column<int>(type: "integer", nullable: true)
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
                name: "subactions_fishingtournamenteligiblecatch",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_fishingtournamenteligiblecatch", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_fishingtournamenteligiblecatch_Actions_ActionTyp~",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_fishingtournamentend",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    TournamentId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_fishingtournamentend", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_fishingtournamentend_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_fishingtournamentstart",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    TournamentId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_fishingtournamentstart", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_fishingtournamentstart_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FishingTournamentCatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FishingTournamentId = table.Column<int>(type: "integer", nullable: false),
                    FishCatchId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FishingTournamentCatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FishingTournamentCatches_FishCatches_FishCatchId",
                        column: x => x.FishCatchId,
                        principalTable: "FishCatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FishingTournamentCatches_FishingTournaments_FishingTourname~",
                        column: x => x.FishingTournamentId,
                        principalTable: "FishingTournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FishingTournamentFishTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FishingTournamentId = table.Column<int>(type: "integer", nullable: false),
                    FishTypeId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FishingTournamentFishTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FishingTournamentFishTypes_FishTypes_FishTypeId",
                        column: x => x.FishTypeId,
                        principalTable: "FishTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FishingTournamentFishTypes_FishingTournaments_FishingTourna~",
                        column: x => x.FishingTournamentId,
                        principalTable: "FishingTournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FishingTournamentRewardRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FishingTournamentId = table.Column<int>(type: "integer", nullable: false),
                    ScoreCategory = table.Column<int>(type: "integer", nullable: false),
                    TargetFishTypeId = table.Column<int>(type: "integer", nullable: true),
                    RewardKind = table.Column<int>(type: "integer", nullable: false),
                    Placement = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<long>(type: "bigint", nullable: false),
                    EntryFeePercentage = table.Column<int>(type: "integer", nullable: true),
                    PointTypeId = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false)
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
                        name: "FK_FishingTournamentRewardRules_FishingTournaments_FishingTour~",
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
                name: "IX_FishingTournamentCatches_FishCatchId",
                table: "FishingTournamentCatches",
                column: "FishCatchId");

            migrationBuilder.CreateIndex(
                name: "IX_FishingTournamentCatches_Tournament_Catch",
                table: "FishingTournamentCatches",
                columns: new[] { "FishingTournamentId", "FishCatchId" },
                unique: true);

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
                name: "IX_FishingTournamentRewardRules_Tournament_Category_Placement",
                table: "FishingTournamentRewardRules",
                columns: new[] { "FishingTournamentId", "ScoreCategory", "TargetFishTypeId", "Placement" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FishingTournamentRewardRules_Tournament_Category_Placement_General",
                table: "FishingTournamentRewardRules",
                columns: new[] { "FishingTournamentId", "ScoreCategory", "Placement" },
                unique: true,
                filter: "\"TargetFishTypeId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FishingTournaments_Enabled_Status_StartsAtUtc",
                table: "FishingTournaments",
                columns: new[] { "Enabled", "Status", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FishingTournaments_EntryFeePointTypeId",
                table: "FishingTournaments",
                column: "EntryFeePointTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_fishingtournamenteligiblecatch_ActionTypeId",
                table: "subactions_fishingtournamenteligiblecatch",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_fishingtournamentend_ActionTypeId",
                table: "subactions_fishingtournamentend",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_fishingtournamentstart_ActionTypeId",
                table: "subactions_fishingtournamentstart",
                column: "ActionTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FishingTournamentCatches");

            migrationBuilder.DropTable(
                name: "FishingTournamentFishTypes");

            migrationBuilder.DropTable(
                name: "FishingTournamentRewardRules");

            migrationBuilder.DropTable(
                name: "subactions_fishingtournamenteligiblecatch");

            migrationBuilder.DropTable(
                name: "subactions_fishingtournamentend");

            migrationBuilder.DropTable(
                name: "subactions_fishingtournamentstart");

            migrationBuilder.DropTable(
                name: "FishingTournaments");
        }
    }
}
