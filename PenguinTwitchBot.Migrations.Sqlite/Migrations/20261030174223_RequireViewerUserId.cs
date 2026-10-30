using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class RequireViewerUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"Viewers\" WHERE \"UserId\" = '';");

            migrationBuilder.DropIndex(
                name: "IX_Viewers_UserId",
                table: "Viewers");

            migrationBuilder.CreateIndex(
                name: "IX_Viewers_UserId",
                table: "Viewers",
                column: "UserId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Viewers_UserId_NotEmpty",
                table: "Viewers",
                sql: "\"UserId\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Viewers_UserId",
                table: "Viewers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Viewers_UserId_NotEmpty",
                table: "Viewers");

            migrationBuilder.CreateIndex(
                name: "IX_Viewers_UserId",
                table: "Viewers",
                column: "UserId",
                unique: true,
                filter: "\"UserId\" <> ''");
        }
    }
}
