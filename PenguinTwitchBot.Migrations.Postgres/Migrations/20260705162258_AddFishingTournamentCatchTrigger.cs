using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddFishingTournamentCatchTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "QualifyingPlacementOverride",
                table: "subactions_fishingtournamenteligiblecatch",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequireQualifyingPosition",
                table: "subactions_fishingtournamenteligiblecatch",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QualifyingPlacementOverride",
                table: "subactions_fishingtournamenteligiblecatch");

            migrationBuilder.DropColumn(
                name: "RequireQualifyingPosition",
                table: "subactions_fishingtournamenteligiblecatch");
        }
    }
}
