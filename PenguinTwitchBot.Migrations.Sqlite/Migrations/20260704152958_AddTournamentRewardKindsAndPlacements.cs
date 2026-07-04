using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentRewardKindsAndPlacements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EntryFeePercentage",
                table: "FishingTournamentRewardRules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Placement",
                table: "FishingTournamentRewardRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RewardKind",
                table: "FishingTournamentRewardRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EntryFeePercentage",
                table: "FishingTournamentRewardRules");

            migrationBuilder.DropColumn(
                name: "Placement",
                table: "FishingTournamentRewardRules");

            migrationBuilder.DropColumn(
                name: "RewardKind",
                table: "FishingTournamentRewardRules");
        }
    }
}
