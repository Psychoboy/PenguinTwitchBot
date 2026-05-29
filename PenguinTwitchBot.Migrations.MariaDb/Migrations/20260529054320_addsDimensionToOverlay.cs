using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.MariaDb.Migrations
{
    /// <inheritdoc />
    public partial class addsDimensionToOverlay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CanvasHeight",
                table: "overlay_layouts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CanvasWidth",
                table: "overlay_layouts",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanvasHeight",
                table: "overlay_layouts");

            migrationBuilder.DropColumn(
                name: "CanvasWidth",
                table: "overlay_layouts");
        }
    }
}
