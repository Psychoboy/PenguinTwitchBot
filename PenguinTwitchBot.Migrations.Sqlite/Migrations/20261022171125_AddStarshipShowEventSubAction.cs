using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddStarshipShowEventSubAction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subactions_starship_show_event",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    DurationSeconds = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_starship_show_event", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_starship_show_event_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_starship_show_event_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subactions_starship_show_event_ActionTypeId",
                table: "subactions_starship_show_event",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_starship_show_event_CatchActionTypeId",
                table: "subactions_starship_show_event",
                column: "CatchActionTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subactions_starship_show_event");
        }
    }
}
