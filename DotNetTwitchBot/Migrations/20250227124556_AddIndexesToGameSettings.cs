using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexesToGameSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SettingName",
                table: "GameSettings",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "GameName",
                table: "GameSettings",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_GameSettings_GameName",
                table: "GameSettings",
                column: "GameName");

            migrationBuilder.CreateIndex(
                name: "IX_GameSettings_GameName_SettingName",
                table: "GameSettings",
                columns: new[] { "GameName", "SettingName" });

            migrationBuilder.CreateIndex(
                name: "IX_GameSettings_SettingName",
                table: "GameSettings",
                column: "SettingName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameSettings_GameName",
                table: "GameSettings");

            migrationBuilder.DropIndex(
                name: "IX_GameSettings_GameName_SettingName",
                table: "GameSettings");

            migrationBuilder.DropIndex(
                name: "IX_GameSettings_SettingName",
                table: "GameSettings");

            migrationBuilder.AlterColumn<string>(
                name: "SettingName",
                table: "GameSettings",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "GameName",
                table: "GameSettings",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
