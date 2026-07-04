using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FishingTournamentId = table.Column<int>(type: "integer", nullable: false),
                    FishCatchId = table.Column<int>(type: "integer", nullable: false)
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
                        name: "FK_FishingTournamentCatches_FishingTournaments_FishingTourname~",
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

            migrationBuilder.CreateIndex(
                name: "IX_FishingTournamentRewardRules_Tournament_Category_Placement_General",
                table: "FishingTournamentRewardRules",
                columns: new[] { "FishingTournamentId", "ScoreCategory", "Placement" },
                unique: true,
                filter: "\"TargetFishTypeId\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FishingTournamentRewardRules_Tournament_Category_Placement_General",
                table: "FishingTournamentRewardRules");

            migrationBuilder.DropTable(
                name: "FishingTournamentCatches");
        }
    }
}
