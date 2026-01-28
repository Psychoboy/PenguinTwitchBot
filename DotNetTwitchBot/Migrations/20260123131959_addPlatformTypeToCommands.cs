using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class addPlatformTypeToCommands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlatformTypes",
                table: "PointCommands",
                type: "longtext",
                nullable: false, 
                defaultValue: new List<PlatformType> { PlatformType.Twitch })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PlatformTypes",
                table: "Keywords",
                type: "longtext",
                nullable: false, 
                defaultValue: new List<PlatformType> { PlatformType.Twitch })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PlatformTypes",
                table: "ExternalCommands",
                type: "longtext",
                nullable: false,
                defaultValue: new List<PlatformType> { PlatformType.Twitch })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PlatformTypes",
                table: "DefaultCommands",
                type: "longtext",
                nullable: false,
                defaultValue: new List<PlatformType> { PlatformType.Twitch })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PlatformTypes",
                table: "CustomCommands",
                type: "longtext",
                nullable: false,
                defaultValue: new List<PlatformType> { PlatformType.Twitch })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PlatformTypes",
                table: "AudioCommands",
                type: "longtext",
                nullable: false,
                defaultValue: new List<PlatformType> { PlatformType.Twitch })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlatformTypes",
                table: "PointCommands");

            migrationBuilder.DropColumn(
                name: "PlatformTypes",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "PlatformTypes",
                table: "ExternalCommands");

            migrationBuilder.DropColumn(
                name: "PlatformTypes",
                table: "DefaultCommands");

            migrationBuilder.DropColumn(
                name: "PlatformTypes",
                table: "CustomCommands");

            migrationBuilder.DropColumn(
                name: "PlatformTypes",
                table: "AudioCommands");
        }
    }
}
