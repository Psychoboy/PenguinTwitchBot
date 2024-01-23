using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class ExcludeCommandsFromChatUI : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExcludeFromUi",
                table: "Keywords",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ExcludeFromUi",
                table: "ExternalCommands",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ExcludeFromUi",
                table: "DefaultCommands",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ExcludeFromUi",
                table: "CustomCommands",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ExcludeFromUi",
                table: "AudioCommands",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExcludeFromUi",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "ExcludeFromUi",
                table: "ExternalCommands");

            migrationBuilder.DropColumn(
                name: "ExcludeFromUi",
                table: "DefaultCommands");

            migrationBuilder.DropColumn(
                name: "ExcludeFromUi",
                table: "CustomCommands");

            migrationBuilder.DropColumn(
                name: "ExcludeFromUi",
                table: "AudioCommands");
        }
    }
}
