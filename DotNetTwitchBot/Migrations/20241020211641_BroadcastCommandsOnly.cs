using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class BroadcastCommandsOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RunFromBroadcasterOnly",
                table: "Keywords",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RunFromBroadcasterOnly",
                table: "ExternalCommands",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RunFromBroadcasterOnly",
                table: "DefaultCommands",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RunFromBroadcasterOnly",
                table: "CustomCommands",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RunFromBroadcasterOnly",
                table: "AudioCommands",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RunFromBroadcasterOnly",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "RunFromBroadcasterOnly",
                table: "ExternalCommands");

            migrationBuilder.DropColumn(
                name: "RunFromBroadcasterOnly",
                table: "DefaultCommands");

            migrationBuilder.DropColumn(
                name: "RunFromBroadcasterOnly",
                table: "CustomCommands");

            migrationBuilder.DropColumn(
                name: "RunFromBroadcasterOnly",
                table: "AudioCommands");
        }
    }
}
