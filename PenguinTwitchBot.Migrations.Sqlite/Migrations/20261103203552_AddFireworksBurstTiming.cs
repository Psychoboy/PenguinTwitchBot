using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddFireworksBurstTiming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DelayVarianceMilliseconds",
                table: "subactions_fireworks",
                type: "TEXT",
                nullable: false,
                defaultValue: "0");

            migrationBuilder.AddColumn<string>(
                name: "StartDelayMilliseconds",
                table: "subactions_fireworks",
                type: "TEXT",
                nullable: false,
                defaultValue: "0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DelayVarianceMilliseconds",
                table: "subactions_fireworks");

            migrationBuilder.DropColumn(
                name: "StartDelayMilliseconds",
                table: "subactions_fireworks");
        }
    }
}
