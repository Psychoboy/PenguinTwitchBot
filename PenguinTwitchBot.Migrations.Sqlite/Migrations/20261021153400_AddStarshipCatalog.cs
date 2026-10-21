using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddStarshipCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "starship_events",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Weight = table.Column<int>(type: "INTEGER", nullable: false),
                    DocumentJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_starship_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "starship_reward_profiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Weight = table.Column<int>(type: "INTEGER", nullable: false),
                    DocumentJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_starship_reward_profiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_starship_events_Enabled_Weight",
                table: "starship_events",
                columns: new[] { "Enabled", "Weight" });

            migrationBuilder.CreateIndex(
                name: "IX_starship_events_Name",
                table: "starship_events",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_starship_reward_profiles_Enabled_Weight",
                table: "starship_reward_profiles",
                columns: new[] { "Enabled", "Weight" });

            migrationBuilder.CreateIndex(
                name: "IX_starship_reward_profiles_Name",
                table: "starship_reward_profiles",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "starship_events");

            migrationBuilder.DropTable(
                name: "starship_reward_profiles");
        }
    }
}
