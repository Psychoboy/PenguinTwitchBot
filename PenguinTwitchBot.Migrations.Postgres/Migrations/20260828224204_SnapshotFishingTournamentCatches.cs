using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
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
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateTime>(
                name: "CaughtAt",
                table: "FishingTournamentCatches",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "FishTypeId",
                table: "FishingTournamentCatches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GoldEarned",
                table: "FishingTournamentCatches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Stars",
                table: "FishingTournamentCatches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "FishingTournamentCatches",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "FishingTournamentCatches",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "Weight",
                table: "FishingTournamentCatches",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateIndex(
                name: "IX_FishingTournamentCatches_Tournament_User",
                table: "FishingTournamentCatches",
                columns: new[] { "FishingTournamentId", "UserId" });

            migrationBuilder.Sql("""
                UPDATE "FishingTournamentCatches" AS ftc
                SET "UserId" = fc."UserId",
                    "Username" = fc."Username",
                    "FishTypeId" = fc."FishTypeId",
                    "Stars" = fc."Stars",
                    "Weight" = fc."Weight",
                    "GoldEarned" = fc."GoldEarned",
                    "CaughtAt" = fc."CaughtAt"
                FROM "FishCatches" AS fc
                WHERE fc."Id" = ftc."FishCatchId";
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
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
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
