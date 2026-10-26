using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddMultipleTimersSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimerName",
                table: "subactions_overlay_timer_stop",
                type: "text",
                nullable: false,
                defaultValue: "Default");

            migrationBuilder.AddColumn<string>(
                name: "TimerName",
                table: "subactions_overlay_timer_start",
                type: "text",
                nullable: false,
                defaultValue: "Default");

            migrationBuilder.AddColumn<string>(
                name: "TimerName",
                table: "subactions_overlay_timer_removetime",
                type: "text",
                nullable: false,
                defaultValue: "Default");

            migrationBuilder.AddColumn<string>(
                name: "TimerName",
                table: "subactions_overlay_timer_addtime",
                type: "text",
                nullable: false,
                defaultValue: "Default");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimerName",
                table: "subactions_overlay_timer_stop");

            migrationBuilder.DropColumn(
                name: "TimerName",
                table: "subactions_overlay_timer_start");

            migrationBuilder.DropColumn(
                name: "TimerName",
                table: "subactions_overlay_timer_removetime");

            migrationBuilder.DropColumn(
                name: "TimerName",
                table: "subactions_overlay_timer_addtime");
        }
    }
}
