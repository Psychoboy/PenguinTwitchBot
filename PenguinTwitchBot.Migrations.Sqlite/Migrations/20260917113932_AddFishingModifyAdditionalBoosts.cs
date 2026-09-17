using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddFishingModifyAdditionalBoosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NewBoostAmount2",
                table: "subactions_fishingmodify",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NewBoostAmount3",
                table: "subactions_fishingmodify",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NewBoostType2",
                table: "subactions_fishingmodify",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NewBoostType3",
                table: "subactions_fishingmodify",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NewBoostAmount2",
                table: "subactions_fishingmodify");

            migrationBuilder.DropColumn(
                name: "NewBoostAmount3",
                table: "subactions_fishingmodify");

            migrationBuilder.DropColumn(
                name: "NewBoostType2",
                table: "subactions_fishingmodify");

            migrationBuilder.DropColumn(
                name: "NewBoostType3",
                table: "subactions_fishingmodify");
        }
    }
}
