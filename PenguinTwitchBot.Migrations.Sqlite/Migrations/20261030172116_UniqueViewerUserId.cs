using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UniqueViewerUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep the most recently seen row per UserId, carrying over a title from any duplicate.
            migrationBuilder.Sql("""
                UPDATE "Viewers" SET "Title" = COALESCE((
                    SELECT MAX(v2."Title") FROM "Viewers" v2
                    WHERE v2."UserId" = "Viewers"."UserId" AND v2."Title" <> ''), "Title")
                WHERE "UserId" <> '' AND "Title" = '';
                """);

            migrationBuilder.Sql("""
                DELETE FROM "Viewers"
                WHERE "UserId" <> '' AND "Id" <> (
                    SELECT v2."Id" FROM "Viewers" v2
                    WHERE v2."UserId" = "Viewers"."UserId"
                    ORDER BY v2."LastSeen" DESC, v2."Id" DESC
                    LIMIT 1);
                """);

            migrationBuilder.DropIndex(
                name: "IX_Viewers_UserId",
                table: "Viewers");

            migrationBuilder.CreateIndex(
                name: "IX_Viewers_UserId",
                table: "Viewers",
                column: "UserId",
                unique: true,
                filter: "\"UserId\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Viewers_UserId",
                table: "Viewers");

            migrationBuilder.CreateIndex(
                name: "IX_Viewers_UserId",
                table: "Viewers",
                column: "UserId");
        }
    }
}
