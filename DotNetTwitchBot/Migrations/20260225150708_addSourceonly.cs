using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class addSourceonly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SourceOnly",
                table: "PointCommands",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SourceOnly",
                table: "Keywords",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SourceOnly",
                table: "ExternalCommands",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SourceOnly",
                table: "DefaultCommands",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SourceOnly",
                table: "CustomCommands",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SourceOnly",
                table: "AudioCommands",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceOnly",
                table: "PointCommands");

            migrationBuilder.DropColumn(
                name: "SourceOnly",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "SourceOnly",
                table: "ExternalCommands");

            migrationBuilder.DropColumn(
                name: "SourceOnly",
                table: "DefaultCommands");

            migrationBuilder.DropColumn(
                name: "SourceOnly",
                table: "CustomCommands");

            migrationBuilder.DropColumn(
                name: "SourceOnly",
                table: "AudioCommands");
        }
    }
}
