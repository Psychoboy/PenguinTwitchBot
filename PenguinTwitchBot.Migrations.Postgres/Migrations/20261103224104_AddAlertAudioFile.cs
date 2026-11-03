using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertAudioFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AudioFile",
                table: "subactions_alert",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioFile",
                table: "subactions_alert");
        }
    }
}
