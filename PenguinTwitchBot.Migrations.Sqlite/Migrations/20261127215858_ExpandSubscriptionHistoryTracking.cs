using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations;

/// <inheritdoc />
public partial class _20261127215858_ExpandSubscriptionHistoryTracking : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ConsecutiveMonths",
            table: "SubscriptionHistories",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "DisplayName",
            table: "SubscriptionHistories",
            type: "TEXT",
            maxLength: 255,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<DateTime>(
            name: "EndedAtUtc",
            table: "SubscriptionHistories",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GifterName",
            table: "SubscriptionHistories",
            type: "TEXT",
            maxLength: 255,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsActive",
            table: "SubscriptionHistories",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsGift",
            table: "SubscriptionHistories",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "Tier",
            table: "SubscriptionHistories",
            type: "TEXT",
            maxLength: 16,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "TotalMonths",
            table: "SubscriptionHistories",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAtUtc",
            table: "SubscriptionHistories",
            type: "TEXT",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

        migrationBuilder.CreateIndex(
            name: "IX_SubscriptionHistories_IsActive_ConsecutiveMonths",
            table: "SubscriptionHistories",
            columns: new[] { "IsActive", "ConsecutiveMonths", "Username" },
            descending: new[] { false, true, false });

        migrationBuilder.CreateIndex(
            name: "IX_SubscriptionHistories_IsActive_TotalMonths",
            table: "SubscriptionHistories",
            columns: new[] { "IsActive", "TotalMonths", "Username" },
            descending: new[] { false, true, false });

        migrationBuilder.CreateIndex(
            name: "IX_SubscriptionHistories_UserId",
            table: "SubscriptionHistories",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_SubscriptionHistories_IsActive_ConsecutiveMonths",
            table: "SubscriptionHistories");

        migrationBuilder.DropIndex(
            name: "IX_SubscriptionHistories_IsActive_TotalMonths",
            table: "SubscriptionHistories");

        migrationBuilder.DropIndex(
            name: "IX_SubscriptionHistories_UserId",
            table: "SubscriptionHistories");

        migrationBuilder.DropColumn(
            name: "ConsecutiveMonths",
            table: "SubscriptionHistories");

        migrationBuilder.DropColumn(
            name: "DisplayName",
            table: "SubscriptionHistories");

        migrationBuilder.DropColumn(
            name: "EndedAtUtc",
            table: "SubscriptionHistories");

        migrationBuilder.DropColumn(
            name: "GifterName",
            table: "SubscriptionHistories");

        migrationBuilder.DropColumn(
            name: "IsActive",
            table: "SubscriptionHistories");

        migrationBuilder.DropColumn(
            name: "IsGift",
            table: "SubscriptionHistories");

        migrationBuilder.DropColumn(
            name: "Tier",
            table: "SubscriptionHistories");

        migrationBuilder.DropColumn(
            name: "TotalMonths",
            table: "SubscriptionHistories");

        migrationBuilder.DropColumn(
            name: "UpdatedAtUtc",
            table: "SubscriptionHistories");
    }
}
