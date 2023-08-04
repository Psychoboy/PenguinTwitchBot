using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDefaultCommands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SayCooldown",
                table: "Keywords",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SayRankRequirement",
                table: "Keywords",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SayCooldown",
                table: "DefaultCommands",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SayRankRequirement",
                table: "DefaultCommands",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SayCooldown",
                table: "CustomCommands",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SayRankRequirement",
                table: "CustomCommands",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SayCooldown",
                table: "AudioCommands",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SayRankRequirement",
                table: "AudioCommands",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SayCooldown",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "SayRankRequirement",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "SayCooldown",
                table: "DefaultCommands");

            migrationBuilder.DropColumn(
                name: "SayRankRequirement",
                table: "DefaultCommands");

            migrationBuilder.DropColumn(
                name: "SayCooldown",
                table: "CustomCommands");

            migrationBuilder.DropColumn(
                name: "SayRankRequirement",
                table: "CustomCommands");

            migrationBuilder.DropColumn(
                name: "SayCooldown",
                table: "AudioCommands");

            migrationBuilder.DropColumn(
                name: "SayRankRequirement",
                table: "AudioCommands");
        }
    }
}
