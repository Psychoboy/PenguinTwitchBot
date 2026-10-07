using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LanguageCode = table.Column<string>(type: "text", nullable: true),
                    Sex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRegisteredVoices", x => x.Id);
                });

            migrationBuilder.Sql("INSERT INTO \"UserRegisteredVoices\" (\"Username\", \"Type\", \"Name\", \"LanguageCode\", \"Sex\") SELECT \"Username\", \"Type\", \"Name\", \"LanguageCode\", \"Sex\" FROM \"RegisteredVoices\" WHERE \"Discriminator\" = 'UserRegisteredVoice';");
            migrationBuilder.Sql("DELETE FROM \"RegisteredVoices\" WHERE \"Discriminator\" = 'UserRegisteredVoice';");

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
                type: "character varying(21)",
                maxLength: 21,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "RegisteredVoices",
                type: "text",
                nullable: true);
        }
    }
}
