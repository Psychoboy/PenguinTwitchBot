using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddDropGamesSessionSubActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subactions_start_drop_game_session",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    GameType = table.Column<string>(type: "TEXT", nullable: false),
                    DurationSeconds = table.Column<string>(type: "TEXT", nullable: false),
                    ThemeOverrideId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_start_drop_game_session", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_start_drop_game_session_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_start_drop_game_session_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_stop_drop_game_session",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    GameType = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_stop_drop_game_session", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_stop_drop_game_session_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_stop_drop_game_session_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subactions_start_drop_game_session_ActionTypeId",
                table: "subactions_start_drop_game_session",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_start_drop_game_session_CatchActionTypeId",
                table: "subactions_start_drop_game_session",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_stop_drop_game_session_ActionTypeId",
                table: "subactions_stop_drop_game_session",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_stop_drop_game_session_CatchActionTypeId",
                table: "subactions_stop_drop_game_session",
                column: "CatchActionTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subactions_start_drop_game_session");

            migrationBuilder.DropTable(
                name: "subactions_stop_drop_game_session");
        }
    }
}
