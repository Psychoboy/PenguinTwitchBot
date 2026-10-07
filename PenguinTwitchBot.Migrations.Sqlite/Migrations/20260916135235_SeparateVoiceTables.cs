using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class SeparateVoiceTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserRegisteredVoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    LanguageCode = table.Column<string>(type: "TEXT", nullable: true),
                    Sex = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRegisteredVoices", x => x.Id);
                });

            migrationBuilder.Sql("INSERT INTO UserRegisteredVoices (Username, Type, Name, LanguageCode, Sex) SELECT Username, Type, Name, LanguageCode, Sex FROM RegisteredVoices WHERE Discriminator = 'UserRegisteredVoice'");
            migrationBuilder.Sql("DELETE FROM RegisteredVoices WHERE Discriminator = 'UserRegisteredVoice'");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "RegisteredVoices");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "RegisteredVoices");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.DropTable(
                name: "UserRegisteredVoices");

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "RegisteredVoices",
                type: "TEXT",
                maxLength: 21,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "RegisteredVoices",
                type: "TEXT",
                nullable: true);
        }
    }
}
