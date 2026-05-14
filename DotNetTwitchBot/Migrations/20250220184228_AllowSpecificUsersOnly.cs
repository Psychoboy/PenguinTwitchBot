using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class AllowSpecificUsersOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SpecificRanks",
                table: "Keywords",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SpecificUserOnly",
                table: "Keywords",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SpecificUsersOnly",
                table: "Keywords",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SpecificRanks",
                table: "ExternalCommands",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SpecificUserOnly",
                table: "ExternalCommands",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SpecificUsersOnly",
                table: "ExternalCommands",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SpecificRanks",
                table: "DefaultCommands",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SpecificUserOnly",
                table: "DefaultCommands",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SpecificUsersOnly",
                table: "DefaultCommands",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SpecificRanks",
                table: "CustomCommands",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SpecificUserOnly",
                table: "CustomCommands",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SpecificUsersOnly",
                table: "CustomCommands",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SpecificRanks",
                table: "AudioCommands",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SpecificUserOnly",
                table: "AudioCommands",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SpecificUsersOnly",
                table: "AudioCommands",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpecificRanks",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "SpecificUserOnly",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "SpecificUsersOnly",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "SpecificRanks",
                table: "ExternalCommands");

            migrationBuilder.DropColumn(
                name: "SpecificUserOnly",
                table: "ExternalCommands");

            migrationBuilder.DropColumn(
                name: "SpecificUsersOnly",
                table: "ExternalCommands");

            migrationBuilder.DropColumn(
                name: "SpecificRanks",
                table: "DefaultCommands");

            migrationBuilder.DropColumn(
                name: "SpecificUserOnly",
                table: "DefaultCommands");

            migrationBuilder.DropColumn(
                name: "SpecificUsersOnly",
                table: "DefaultCommands");

            migrationBuilder.DropColumn(
                name: "SpecificRanks",
                table: "CustomCommands");

            migrationBuilder.DropColumn(
                name: "SpecificUserOnly",
                table: "CustomCommands");

            migrationBuilder.DropColumn(
                name: "SpecificUsersOnly",
                table: "CustomCommands");

            migrationBuilder.DropColumn(
                name: "SpecificRanks",
                table: "AudioCommands");

            migrationBuilder.DropColumn(
                name: "SpecificUserOnly",
                table: "AudioCommands");

            migrationBuilder.DropColumn(
                name: "SpecificUsersOnly",
                table: "AudioCommands");
        }
    }
}
