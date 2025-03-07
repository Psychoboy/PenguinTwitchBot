using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class removeOldTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS viewerpointwithranks");
            migrationBuilder.Sql("DROP VIEW IF EXISTS viewerticketwithranks");
            migrationBuilder.DropTable(
                name: "ViewerPoints");

            migrationBuilder.DropTable(
                name: "ViewerTickets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ViewerPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Points = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Username = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    banned = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViewerPoints", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ViewerTickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Points = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Username = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    banned = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViewerTickets", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ViewerPoints_UserId",
                table: "ViewerPoints",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ViewerPoints_Username",
                table: "ViewerPoints",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_ViewerTickets_UserId",
                table: "ViewerTickets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ViewerTickets_Username",
                table: "ViewerTickets",
                column: "Username");
        }
    }
}
