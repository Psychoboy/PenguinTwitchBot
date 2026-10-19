using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddBalloonAscentSubActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subactions_balloon_ascent",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    Username = table.Column<string>(type: "text", nullable: false),
                    EmoteOrImage = table.Column<string>(type: "text", nullable: false),
                    ThemeOverrideId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_balloon_ascent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_balloon_ascent_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_balloon_ascent_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_balloon_hazards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    HazardType = table.Column<string>(type: "text", nullable: false),
                    SizePreset = table.Column<string>(type: "text", nullable: false),
                    SpeedPreset = table.Column<string>(type: "text", nullable: false),
                    SpawnPattern = table.Column<string>(type: "text", nullable: false),
                    EmoteOrImage = table.Column<string>(type: "text", nullable: false),
                    TargetUser = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_balloon_hazards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_balloon_hazards_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_balloon_hazards_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subactions_balloon_ascent_ActionTypeId",
                table: "subactions_balloon_ascent",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_balloon_ascent_CatchActionTypeId",
                table: "subactions_balloon_ascent",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_balloon_hazards_ActionTypeId",
                table: "subactions_balloon_hazards",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_balloon_hazards_CatchActionTypeId",
                table: "subactions_balloon_hazards",
                column: "CatchActionTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subactions_balloon_ascent");

            migrationBuilder.DropTable(
                name: "subactions_balloon_hazards");
        }
    }
}
