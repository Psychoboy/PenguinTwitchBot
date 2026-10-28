using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using PenguinTwitchBot.Database.Bot.Core.Database;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <summary>
    /// Runs before <c>MigrateAllIdsToUuidV7</c>. The table rebuilds in that migration copy rows with foreign
    /// key enforcement on, so any legacy row pointing at a parent that no longer exists (an orphan) would abort
    /// the whole upgrade. Orphans are meaningless data: rows with a required dangling reference are deleted and
    /// optional dangling references are set to NULL.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261027233000_CleanOrphanedRowsBeforeUuidMigration")]
    public partial class CleanOrphanedRowsBeforeUuidMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA defer_foreign_keys = ON;");

            var model = new MigrateAllIdsToUuidV7().TargetModel;
            var statements = new System.Collections.Generic.List<string>();

            foreach (var entityType in model.GetEntityTypes())
            {
                var table = entityType.GetTableName();
                if (table == null || entityType.GetViewName() != null)
                {
                    continue;
                }

                foreach (var fk in entityType.GetForeignKeys())
                {
                    var principalTable = fk.PrincipalEntityType.GetTableName();
                    if (principalTable == null || fk.Properties.Count != 1)
                    {
                        continue;
                    }

                    var child = LegacyTableName(table);
                    var parent = LegacyTableName(principalTable);
                    var col = fk.Properties[0].GetColumnName(StoreObjectIdentifier.Table(table, entityType.GetSchema()));
                    var parentCol = fk.PrincipalKey.Properties[0].GetColumnName(StoreObjectIdentifier.Table(principalTable, fk.PrincipalEntityType.GetSchema()));
                    var dangling = $"\"{col}\" IS NOT NULL AND \"{col}\" NOT IN (SELECT \"{parentCol}\" FROM \"{parent}\")";

                    statements.Add(fk.Properties[0].IsNullable
                        ? $"UPDATE \"{child}\" SET \"{col}\" = NULL WHERE {dangling};"
                        : $"DELETE FROM \"{child}\" WHERE {dangling};");
                }
            }

            // Deleting an orphan can orphan its own children, so repeat until the chain settles.
            for (var pass = 0; pass < 4; pass++)
            {
                foreach (var statement in statements.Distinct())
                {
                    migrationBuilder.Sql(statement);
                }
            }
        }

        // This table was renamed by MigrateAllIdsToUuidV7, which runs after this migration.
        private static string LegacyTableName(string table) =>
            table == "FishingTournamentEligibleCategories" ? "FishingTournamentEligibleCategory" : table;

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

