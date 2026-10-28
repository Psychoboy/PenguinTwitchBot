using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PenguinTwitchBot.Database.Bot.Core.Database;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
{
    // Game point-type assignments lived in SettingIntValue; the code now reads a Guid from SettingStringValue.
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261028000300_ConvertGameSettingPointTypeIds")]
    public partial class ConvertGameSettingPointTypeIds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ""GameSettings""
SET ""SettingStringValue"" = '00000000-0000-7000-8000-' || lpad(to_hex(""SettingIntValue""), 12, '0')
WHERE lower(""SettingName"") = 'pointtypeid' AND ""SettingIntValue"" > 0
  AND (""SettingStringValue"" IS NULL OR length(""SettingStringValue"") < 36);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
