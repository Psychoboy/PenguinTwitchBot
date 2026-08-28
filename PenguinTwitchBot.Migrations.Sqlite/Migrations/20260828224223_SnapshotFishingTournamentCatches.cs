using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class SnapshotFishingTournamentCatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FishingTournamentCatches_FishCatches_FishCatchId",
                table: "FishingTournamentCatches");

            migrationBuilder.AlterColumn<int>(
                name: "FishCatchId",
                table: "FishingTournamentCatches",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<DateTime>(
                name: "CaughtAt",
                table: "FishingTournamentCatches",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "FishTypeId",
                table: "FishingTournamentCatches",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GoldEarned",
                table: "FishingTournamentCatches",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Stars",
                table: "FishingTournamentCatches",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "FishingTournamentCatches",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "FishingTournamentCatches",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "Weight",
                table: "FishingTournamentCatches",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateIndex(
                name: "IX_FishingTournamentCatches_Tournament_User",
                table: "FishingTournamentCatches",
                columns: new[] { "FishingTournamentId", "UserId" });

            migrationBuilder.Sql("""
                UPDATE "FishingTournamentCatches"
                SET "UserId" = (SELECT fc."UserId" FROM "FishCatches" fc WHERE fc."Id" = "FishingTournamentCatches"."FishCatchId"),
                    "Username" = (SELECT fc."Username" FROM "FishCatches" fc WHERE fc."Id" = "FishingTournamentCatches"."FishCatchId"),
                    "FishTypeId" = (SELECT fc."FishTypeId" FROM "FishCatches" fc WHERE fc."Id" = "FishingTournamentCatches"."FishCatchId"),
                    "Stars" = (SELECT fc."Stars" FROM "FishCatches" fc WHERE fc."Id" = "FishingTournamentCatches"."FishCatchId"),
                    "Weight" = (SELECT fc."Weight" FROM "FishCatches" fc WHERE fc."Id" = "FishingTournamentCatches"."FishCatchId"),
                    "GoldEarned" = (SELECT fc."GoldEarned" FROM "FishCatches" fc WHERE fc."Id" = "FishingTournamentCatches"."FishCatchId"),
                    "CaughtAt" = (SELECT fc."CaughtAt" FROM "FishCatches" fc WHERE fc."Id" = "FishingTournamentCatches"."FishCatchId")
                WHERE EXISTS (SELECT 1 FROM "FishCatches" fc WHERE fc."Id" = "FishingTournamentCatches"."FishCatchId");
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_FishingTournamentCatches_FishCatches_FishCatchId",
                table: "FishingTournamentCatches",
                column: "FishCatchId",
                principalTable: "FishCatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FishingTournamentCatches_FishCatches_FishCatchId",
                table: "FishingTournamentCatches");

            migrationBuilder.DropIndex(
                name: "IX_FishingTournamentCatches_Tournament_User",
                table: "FishingTournamentCatches");

            migrationBuilder.DropColumn(
                name: "CaughtAt",
                table: "FishingTournamentCatches");

            migrationBuilder.DropColumn(
                name: "FishTypeId",
                table: "FishingTournamentCatches");

            migrationBuilder.DropColumn(
                name: "GoldEarned",
                table: "FishingTournamentCatches");

            migrationBuilder.DropColumn(
                name: "Stars",
                table: "FishingTournamentCatches");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "FishingTournamentCatches");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "FishingTournamentCatches");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "FishingTournamentCatches");

            migrationBuilder.AlterColumn<int>(
                name: "FishCatchId",
                table: "FishingTournamentCatches",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FishingTournamentCatches_FishCatches_FishCatchId",
                table: "FishingTournamentCatches",
                column: "FishCatchId",
                principalTable: "FishCatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
