using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class FishCategoriesDbSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FishCategory_FishTypes_FishTypeId",
                table: "FishCategory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FishCategory",
                table: "FishCategory");

            migrationBuilder.RenameTable(
                name: "FishCategory",
                newName: "FishCategories");

            migrationBuilder.RenameIndex(
                name: "IX_FishCategory_FishTypeId",
                table: "FishCategories",
                newName: "IX_FishCategories_FishTypeId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FishCategories",
                table: "FishCategories",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FishCategories_FishTypes_FishTypeId",
                table: "FishCategories",
                column: "FishTypeId",
                principalTable: "FishTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FishCategories_FishTypes_FishTypeId",
                table: "FishCategories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FishCategories",
                table: "FishCategories");

            migrationBuilder.RenameTable(
                name: "FishCategories",
                newName: "FishCategory");

            migrationBuilder.RenameIndex(
                name: "IX_FishCategories_FishTypeId",
                table: "FishCategory",
                newName: "IX_FishCategory_FishTypeId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FishCategory",
                table: "FishCategory",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FishCategory_FishTypes_FishTypeId",
                table: "FishCategory",
                column: "FishTypeId",
                principalTable: "FishTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
