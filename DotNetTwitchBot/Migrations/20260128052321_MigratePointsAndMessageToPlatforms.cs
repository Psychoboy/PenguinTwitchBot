using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class MigratePointsAndMessageToPlatforms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Platform",
                table: "Viewers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Platform",
                table: "UserPoints",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Platforms",
                table: "TimerGroups",
                type: "longtext",
                nullable: false,
                defaultValue: new List<PlatformType> { PlatformType.Twitch })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "Platform",
                table: "Cooldowns",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Platform",
                table: "Viewers");

            migrationBuilder.DropColumn(
                name: "Platform",
                table: "UserPoints");

            migrationBuilder.DropColumn(
                name: "Platforms",
                table: "TimerGroups");

            migrationBuilder.DropColumn(
                name: "Platform",
                table: "Cooldowns");
        }
    }
}
