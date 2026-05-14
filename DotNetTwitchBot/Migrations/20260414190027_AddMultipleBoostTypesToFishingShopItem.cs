using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class AddMultipleBoostTypesToFishingShopItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "BoostAmount2",
                table: "FishingShopItems",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BoostAmount3",
                table: "FishingShopItems",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BoostType2",
                table: "FishingShopItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BoostType3",
                table: "FishingShopItems",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BoostAmount2",
                table: "FishingShopItems");

            migrationBuilder.DropColumn(
                name: "BoostAmount3",
                table: "FishingShopItems");

            migrationBuilder.DropColumn(
                name: "BoostType2",
                table: "FishingShopItems");

            migrationBuilder.DropColumn(
                name: "BoostType3",
                table: "FishingShopItems");
        }
    }
}
