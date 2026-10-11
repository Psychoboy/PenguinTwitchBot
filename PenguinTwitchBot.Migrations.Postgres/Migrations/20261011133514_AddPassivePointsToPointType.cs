using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddPassivePointsToPointType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PassivePointsActiveViewers",
                table: "PointTypes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "PassivePointsAllViewers",
                table: "PointTypes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "PassivePointsEnabled",
                table: "PointTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PassivePointsIntervalMinutes",
                table: "PointTypes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "PassivePointsSubscribers",
                table: "PointTypes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PassivePointsActiveViewers",
                table: "PointTypes");

            migrationBuilder.DropColumn(
                name: "PassivePointsAllViewers",
                table: "PointTypes");

            migrationBuilder.DropColumn(
                name: "PassivePointsEnabled",
                table: "PointTypes");

            migrationBuilder.DropColumn(
                name: "PassivePointsIntervalMinutes",
                table: "PointTypes");

            migrationBuilder.DropColumn(
                name: "PassivePointsSubscribers",
                table: "PointTypes");
        }
    }
}
