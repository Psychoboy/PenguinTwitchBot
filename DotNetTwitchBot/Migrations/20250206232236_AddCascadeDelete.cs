using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class AddCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Songs_Playlists_MusicPlaylistId",
                table: "Songs");

            migrationBuilder.DropForeignKey(
                name: "FK_TimerMessages_TimerGroups_TimerGroupId",
                table: "TimerMessages");

            migrationBuilder.AlterColumn<int>(
                name: "TimerGroupId",
                table: "TimerMessages",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MusicPlaylistId",
                table: "Songs",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Songs_Playlists_MusicPlaylistId",
                table: "Songs",
                column: "MusicPlaylistId",
                principalTable: "Playlists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TimerMessages_TimerGroups_TimerGroupId",
                table: "TimerMessages",
                column: "TimerGroupId",
                principalTable: "TimerGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Songs_Playlists_MusicPlaylistId",
                table: "Songs");

            migrationBuilder.DropForeignKey(
                name: "FK_TimerMessages_TimerGroups_TimerGroupId",
                table: "TimerMessages");

            migrationBuilder.AlterColumn<int>(
                name: "TimerGroupId",
                table: "TimerMessages",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "MusicPlaylistId",
                table: "Songs",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_Songs_Playlists_MusicPlaylistId",
                table: "Songs",
                column: "MusicPlaylistId",
                principalTable: "Playlists",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TimerMessages_TimerGroups_TimerGroupId",
                table: "TimerMessages",
                column: "TimerGroupId",
                principalTable: "TimerGroups",
                principalColumn: "Id");
        }
    }
}
