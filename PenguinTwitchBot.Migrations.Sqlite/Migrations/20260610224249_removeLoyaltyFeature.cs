using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class removeLoyaltyFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "GameSettings",
                keyColumn: "GameName",
                keyValue: "loyaltyfeature"
            );

            migrationBuilder.DeleteData(
                table: "GameSettings",
                keyColumn: "GameName",
                keyValue: "ticketsfeature"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
