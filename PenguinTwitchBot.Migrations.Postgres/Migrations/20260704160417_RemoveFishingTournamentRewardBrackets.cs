using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFishingTournamentRewardBrackets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FishingTournamentRewardRules_Tournament_Category_Bracket",
                table: "FishingTournamentRewardRules");

            migrationBuilder.DropColumn(
                name: "MaxPlacement",
                table: "FishingTournamentRewardRules");

            migrationBuilder.DropColumn(
                name: "MinPlacement",
                table: "FishingTournamentRewardRules");

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

            migrationBuilder.CreateIndex(
                name: "IX_FishingTournamentRewardRules_Tournament_Category_Placement",
                table: "FishingTournamentRewardRules",
                columns: new[] { "FishingTournamentId", "ScoreCategory", "TargetFishTypeId", "Placement" },
                unique: true);

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
                name: "subactions_fishingtournamentend");

            migrationBuilder.DropTable(
                name: "subactions_fishingtournamentstart");

            migrationBuilder.DropIndex(
                name: "IX_FishingTournamentRewardRules_Tournament_Category_Placement",
                table: "FishingTournamentRewardRules");

            migrationBuilder.AddColumn<int>(
                name: "MaxPlacement",
                table: "FishingTournamentRewardRules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinPlacement",
                table: "FishingTournamentRewardRules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_FishingTournamentRewardRules_Tournament_Category_Bracket",
                table: "FishingTournamentRewardRules",
                columns: new[] { "FishingTournamentId", "ScoreCategory", "TargetFishTypeId", "MinPlacement", "MaxPlacement" },
                unique: true);
        }
    }
}
