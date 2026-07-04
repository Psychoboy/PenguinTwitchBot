using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddFishingTournamentCatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FishingTournamentCatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FishingTournamentId = table.Column<int>(type: "INTEGER", nullable: false),
                    FishCatchId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FishingTournamentCatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FishingTournamentCatches_FishCatches_FishCatchId",
                        column: x => x.FishCatchId,
                        principalTable: "FishCatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FishingTournamentCatches_FishingTournaments_FishingTournamentId",
                        column: x => x.FishingTournamentId,
                        principalTable: "FishingTournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FishingTournamentCatches_FishCatchId",
                table: "FishingTournamentCatches",
                column: "FishCatchId");

            migrationBuilder.CreateIndex(
                name: "IX_FishingTournamentCatches_Tournament_Catch",
                table: "FishingTournamentCatches",
                columns: new[] { "FishingTournamentId", "FishCatchId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FishingTournamentCatches");
        }
    }
}
