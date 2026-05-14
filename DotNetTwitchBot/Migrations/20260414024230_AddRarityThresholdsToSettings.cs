using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class AddRarityThresholdsToSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RarityEpicThreshold",
                table: "FishingSettings",
                type: "int",
                nullable: false,
                defaultValue: 110);

            migrationBuilder.AddColumn<int>(
                name: "RarityLegendaryThreshold",
                table: "FishingSettings",
                type: "int",
                nullable: false,
                defaultValue: 201);

            migrationBuilder.AddColumn<int>(
                name: "RarityRareThreshold",
                table: "FishingSettings",
                type: "int",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<int>(
                name: "RarityUncommonThreshold",
                table: "FishingSettings",
                type: "int",
                nullable: false,
                defaultValue: 35);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RarityEpicThreshold",
                table: "FishingSettings");

            migrationBuilder.DropColumn(
                name: "RarityLegendaryThreshold",
                table: "FishingSettings");

            migrationBuilder.DropColumn(
                name: "RarityRareThreshold",
                table: "FishingSettings");

            migrationBuilder.DropColumn(
                name: "RarityUncommonThreshold",
                table: "FishingSettings");
        }
    }
}
