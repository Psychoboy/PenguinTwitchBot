using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddSongRequestHistoryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SongRequestHistories_SongId_RequestDate",
                table: "SongRequestHistories",
                columns: new[] { "SongId", "RequestDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SongRequestHistories_SongId_RequestDate",
                table: "SongRequestHistories");
        }
    }
}
