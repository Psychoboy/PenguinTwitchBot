using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class addMaxCoolDown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GlobalCooldownMax",
                table: "PointCommands",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserCooldownMax",
                table: "PointCommands",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GlobalCooldownMax",
                table: "Keywords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserCooldownMax",
                table: "Keywords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GlobalCooldownMax",
                table: "ExternalCommands",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserCooldownMax",
                table: "ExternalCommands",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GlobalCooldownMax",
                table: "DefaultCommands",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserCooldownMax",
                table: "DefaultCommands",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GlobalCooldownMax",
                table: "AudioCommands",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserCooldownMax",
                table: "AudioCommands",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GlobalCooldownMax",
                table: "ActionCommands",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserCooldownMax",
                table: "ActionCommands",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GlobalCooldownMax",
                table: "PointCommands");

            migrationBuilder.DropColumn(
                name: "UserCooldownMax",
                table: "PointCommands");

            migrationBuilder.DropColumn(
                name: "GlobalCooldownMax",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "UserCooldownMax",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "GlobalCooldownMax",
                table: "ExternalCommands");

            migrationBuilder.DropColumn(
                name: "UserCooldownMax",
                table: "ExternalCommands");

            migrationBuilder.DropColumn(
                name: "GlobalCooldownMax",
                table: "DefaultCommands");

            migrationBuilder.DropColumn(
                name: "UserCooldownMax",
                table: "DefaultCommands");

            migrationBuilder.DropColumn(
                name: "GlobalCooldownMax",
                table: "AudioCommands");

            migrationBuilder.DropColumn(
                name: "UserCooldownMax",
                table: "AudioCommands");

            migrationBuilder.DropColumn(
                name: "GlobalCooldownMax",
                table: "ActionCommands");

            migrationBuilder.DropColumn(
                name: "UserCooldownMax",
                table: "ActionCommands");
        }
    }
}
