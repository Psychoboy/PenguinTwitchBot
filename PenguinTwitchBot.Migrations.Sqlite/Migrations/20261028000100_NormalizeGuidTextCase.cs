using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PenguinTwitchBot.Database.Bot.Core.Database;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <summary>
    /// Microsoft.Data.Sqlite stores and compares Guids as UPPERCASE text, and SQLite text comparison is
    /// case-sensitive. The first version of the legacy-ID conversion wrote lowercase hex (ids 10, 11, ...
    /// became ...0a, ...0b), so lookups and UPDATEs by Guid silently matched nothing for those rows.
    /// Upper-casing every Guid key column fixes databases that already ran it; it is a no-op otherwise.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261028000100_NormalizeGuidTextCase")]
    public partial class NormalizeGuidTextCase : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA defer_foreign_keys = ON;");
            foreach (var (table, col) in MigrateAllIdsToUuidV7Helper.GetGuidKeyColumns(new MigrateAllIdsToUuidV7().TargetModel))
            {
                migrationBuilder.Sql($"UPDATE \"{table}\" SET \"{col}\" = upper(\"{col}\") WHERE \"{col}\" IS NOT NULL AND \"{col}\" <> upper(\"{col}\");");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

