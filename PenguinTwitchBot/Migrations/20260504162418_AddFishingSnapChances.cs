using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class AddFishingSnapChances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "LineSnapChance",
                table: "FishingSettings",
                type: "double",
                nullable: false,
                defaultValue: 0.02);

            migrationBuilder.AddColumn<double>(
                name: "RodSnapChance",
                table: "FishingSettings",
                type: "double",
                nullable: false,
                defaultValue: 0.005);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LineSnapChance",
                table: "FishingSettings");

            migrationBuilder.DropColumn(
                name: "RodSnapChance",
                table: "FishingSettings");
        }
    }
}
