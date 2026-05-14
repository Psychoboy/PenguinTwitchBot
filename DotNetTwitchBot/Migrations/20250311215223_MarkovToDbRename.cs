using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class MarkovToDbRename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Key",
                table: "MarkovValues",
                newName: "KeyIndex");

            migrationBuilder.RenameIndex(
                name: "IX_MarkovValues_Key",
                table: "MarkovValues",
                newName: "IX_MarkovValues_KeyIndex");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "KeyIndex",
                table: "MarkovValues",
                newName: "Key");

            migrationBuilder.RenameIndex(
                name: "IX_MarkovValues_KeyIndex",
                table: "MarkovValues",
                newName: "IX_MarkovValues_Key");
        }
    }
}
