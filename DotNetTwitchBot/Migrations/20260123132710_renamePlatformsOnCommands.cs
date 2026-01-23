using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class renamePlatformsOnCommands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PlatformTypes",
                table: "PointCommands",
                newName: "Platforms");

            migrationBuilder.RenameColumn(
                name: "PlatformTypes",
                table: "Keywords",
                newName: "Platforms");

            migrationBuilder.RenameColumn(
                name: "PlatformTypes",
                table: "ExternalCommands",
                newName: "Platforms");

            migrationBuilder.RenameColumn(
                name: "PlatformTypes",
                table: "DefaultCommands",
                newName: "Platforms");

            migrationBuilder.RenameColumn(
                name: "PlatformTypes",
                table: "CustomCommands",
                newName: "Platforms");

            migrationBuilder.RenameColumn(
                name: "PlatformTypes",
                table: "AudioCommands",
                newName: "Platforms");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Platforms",
                table: "PointCommands",
                newName: "PlatformTypes");

            migrationBuilder.RenameColumn(
                name: "Platforms",
                table: "Keywords",
                newName: "PlatformTypes");

            migrationBuilder.RenameColumn(
                name: "Platforms",
                table: "ExternalCommands",
                newName: "PlatformTypes");

            migrationBuilder.RenameColumn(
                name: "Platforms",
                table: "DefaultCommands",
                newName: "PlatformTypes");

            migrationBuilder.RenameColumn(
                name: "Platforms",
                table: "CustomCommands",
                newName: "PlatformTypes");

            migrationBuilder.RenameColumn(
                name: "Platforms",
                table: "AudioCommands",
                newName: "PlatformTypes");
        }
    }
}
