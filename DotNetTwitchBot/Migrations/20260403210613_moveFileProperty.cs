using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class moveFileProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "File",
                table: "subactions_watchtime");

            migrationBuilder.DropColumn(
                name: "File",
                table: "subactions_uptime");

            migrationBuilder.DropColumn(
                name: "File",
                table: "subactions_tts");

            migrationBuilder.DropColumn(
                name: "File",
                table: "subactions_sendmessage");

            migrationBuilder.DropColumn(
                name: "File",
                table: "subactions_replytomessage");

            migrationBuilder.DropColumn(
                name: "File",
                table: "subactions_randomint");

            migrationBuilder.DropColumn(
                name: "File",
                table: "subactions_multicounter");

            migrationBuilder.DropColumn(
                name: "File",
                table: "subactions_giveawayprize");

            migrationBuilder.DropColumn(
                name: "File",
                table: "subactions_followage");

            migrationBuilder.DropColumn(
                name: "File",
                table: "subactions_externalapi");

            migrationBuilder.DropColumn(
                name: "File",
                table: "subactions_currenttime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "File",
                table: "subactions_watchtime",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "File",
                table: "subactions_uptime",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "File",
                table: "subactions_tts",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "File",
                table: "subactions_sendmessage",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "File",
                table: "subactions_replytomessage",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "File",
                table: "subactions_randomint",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "File",
                table: "subactions_multicounter",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "File",
                table: "subactions_giveawayprize",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "File",
                table: "subactions_followage",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "File",
                table: "subactions_externalapi",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "File",
                table: "subactions_currenttime",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
