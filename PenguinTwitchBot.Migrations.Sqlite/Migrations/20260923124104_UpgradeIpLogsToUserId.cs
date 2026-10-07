using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeIpLogsToUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "IpLogEntrys"
                SET "UserId" = (
                    SELECT v."UserId"
                    FROM "Viewers" v
                    WHERE lower(v."Username") = lower("IpLogEntrys"."Username") AND v."UserId" IS NOT NULL AND v."UserId" != ''
                    LIMIT 1
                )
                WHERE ("UserId" IS NULL OR "UserId" = '')
                  AND EXISTS (
                    SELECT 1
                    FROM "Viewers" v
                    WHERE lower(v."Username") = lower("IpLogEntrys"."Username") AND v."UserId" IS NOT NULL AND v."UserId" != ''
                  );

                DELETE FROM "IpLogEntrys" WHERE "UserId" IS NULL OR trim("UserId") = '' OR lower("Username") = 'anonymous';

                CREATE TEMP TABLE "_IpLogMerged" AS
                SELECT
                    min("Id") AS "Id",
                    "UserId",
                    "Ip",
                    max("ConnectedDate") AS "ConnectedDate",
                    sum("Count") AS "Count",
                    (SELECT i2."Username" FROM "IpLogEntrys" i2 WHERE i2."UserId" = i1."UserId" AND i2."Ip" = i1."Ip" ORDER BY i2."ConnectedDate" DESC, i2."Id" DESC LIMIT 1) AS "Username"
                FROM "IpLogEntrys" i1
                GROUP BY "UserId", "Ip";

                DELETE FROM "IpLogEntrys";

                INSERT INTO "IpLogEntrys" ("Id", "UserId", "Ip", "Username", "Count", "ConnectedDate")
                SELECT "Id", "UserId", "Ip", "Username", "Count", "ConnectedDate" FROM "_IpLogMerged";

                DROP TABLE "_IpLogMerged";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_IpLogEntrys_UserId",
                table: "IpLogEntrys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_IpLogEntrys_UserId_Ip",
                table: "IpLogEntrys",
                columns: new[] { "UserId", "Ip" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IpLogEntrys_UserId",
                table: "IpLogEntrys");

            migrationBuilder.DropIndex(
                name: "IX_IpLogEntrys_UserId_Ip",
                table: "IpLogEntrys");
        }
    }
}
