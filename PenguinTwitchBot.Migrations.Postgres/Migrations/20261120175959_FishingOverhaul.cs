using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class FishingOverhaul : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FishingTournamentRewardRules_Tournament_Category_Placement",
                table: "FishingTournamentRewardRules");

            migrationBuilder.DropIndex(
                name: "IX_FishingTournamentRewardRules_Tournament_Category_Placement_General",
                table: "FishingTournamentRewardRules");

            migrationBuilder.AddColumn<bool>(
                name: "WaitForReveal",
                table: "subactions_fishing",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AccidentSoundPath",
                table: "FishingSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AnnounceTournamentCountdown",
                table: "FishingSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AnnounceTournamentLeadChanges",
                table: "FishingSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "BiteSoundPath",
                table: "FishingSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CastAnimationMs",
                table: "FishingSettings",
                type: "integer",
                nullable: false,
                defaultValue: 1600);

            migrationBuilder.AddColumn<string>(
                name: "CastSoundPath",
                table: "FishingSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CatchCommonSoundPath",
                table: "FishingSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CatchLegendarySoundPath",
                table: "FishingSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CatchRareSoundPath",
                table: "FishingSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CatchRewardsJson",
                table: "FishingSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableCastAnimation",
                table: "FishingSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "FrenzySoundPath",
                table: "FishingSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuickTournamentRewardsJson",
                table: "FishingSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SoundEnabled",
                table: "FishingSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<double>(
                name: "SoundVolume",
                table: "FishingSettings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.8);

            migrationBuilder.CreateTable(
                name: "subactions_fishing_frenzy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CatchActionTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    FrenzyName = table.Column<string>(type: "text", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    RarityMultiplier = table.Column<double>(type: "double precision", nullable: false),
                    TargetCategory = table.Column<string>(type: "text", nullable: false),
                    CategoryBoost = table.Column<double>(type: "double precision", nullable: false),
                    AnnounceInChat = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_fishing_frenzy", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_fishing_frenzy_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_fishing_frenzy_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_fishing_quick_tournament",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CatchActionTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    Theme = table.Column<string>(type: "text", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    PairWithFrenzy = table.Column<bool>(type: "boolean", nullable: false),
                    FrenzyRarityMultiplier = table.Column<double>(type: "double precision", nullable: false),
                    AnnounceInChat = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_fishing_quick_tournament", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_fishing_quick_tournament_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_fishing_quick_tournament_Actions_CatchActionType~",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserFishingLoadouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SlotItemsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFishingLoadouts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FishingTournamentRewardRules_Tournament_Category_Placement_General_PointType",
                table: "FishingTournamentRewardRules",
                columns: new[] { "FishingTournamentId", "ScoreCategory", "Placement", "PointTypeId" },
                unique: true,
                filter: "\"TargetFishTypeId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FishingTournamentRewardRules_Tournament_Category_Placement_PointType",
                table: "FishingTournamentRewardRules",
                columns: new[] { "FishingTournamentId", "ScoreCategory", "TargetFishTypeId", "Placement", "PointTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subactions_fishing_frenzy_ActionTypeId",
                table: "subactions_fishing_frenzy",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_fishing_frenzy_CatchActionTypeId",
                table: "subactions_fishing_frenzy",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_fishing_quick_tournament_ActionTypeId",
                table: "subactions_fishing_quick_tournament",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_fishing_quick_tournament_CatchActionTypeId",
                table: "subactions_fishing_quick_tournament",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFishingLoadouts_UserId",
                table: "UserFishingLoadouts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subactions_fishing_frenzy");

            migrationBuilder.DropTable(
                name: "subactions_fishing_quick_tournament");

            migrationBuilder.DropTable(
                name: "UserFishingLoadouts");

            migrationBuilder.DropIndex(
                name: "IX_FishingTournamentRewardRules_Tournament_Category_Placement_General_PointType",
                table: "FishingTournamentRewardRules");

            migrationBuilder.DropIndex(
                name: "IX_FishingTournamentRewardRules_Tournament_Category_Placement_PointType",
                table: "FishingTournamentRewardRules");

            migrationBuilder.DropColumn(
                name: "WaitForReveal",
                table: "subactions_fishing");

            migrationBuilder.DropColumn(
                name: "AccidentSoundPath",
                table: "FishingSettings");

            migrationBuilder.DropColumn(
                name: "AnnounceTournamentCountdown",
                table: "FishingSettings");

            migrationBuilder.DropColumn(
                name: "AnnounceTournamentLeadChanges",
                table: "FishingSettings");

            migrationBuilder.DropColumn(
                name: "BiteSoundPath",
                table: "FishingSettings");

            migrationBuilder.DropColumn(
                name: "CastAnimationMs",
                table: "FishingSettings");

            migrationBuilder.DropColumn(
                name: "CastSoundPath",
                table: "FishingSettings");

            migrationBuilder.DropColumn(
                name: "CatchCommonSoundPath",
                table: "FishingSettings");

            migrationBuilder.DropColumn(
                name: "CatchLegendarySoundPath",
                table: "FishingSettings");

            migrationBuilder.DropColumn(
                name: "CatchRareSoundPath",
                table: "FishingSettings");

            migrationBuilder.DropColumn(
                name: "CatchRewardsJson",
                table: "FishingSettings");

            migrationBuilder.DropColumn(
                name: "EnableCastAnimation",
                table: "FishingSettings");

            migrationBuilder.DropColumn(
                name: "FrenzySoundPath",
                table: "FishingSettings");

            migrationBuilder.DropColumn(
                name: "QuickTournamentRewardsJson",
                table: "FishingSettings");

            migrationBuilder.DropColumn(
                name: "SoundEnabled",
                table: "FishingSettings");

            migrationBuilder.DropColumn(
                name: "SoundVolume",
                table: "FishingSettings");

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
        }
    }
}
