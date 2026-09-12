using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddSongCooldowns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SongCooldowns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SongId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CooldownExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AddedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SongCooldowns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SongCooldowns_SongId",
                table: "SongCooldowns",
                column: "SongId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SongCooldowns_SongId",
                table: "SongCooldowns");

            migrationBuilder.DropTable(
                name: "SongCooldowns");
        }
    }
}
