using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddOverlayTimerVisibilityAndZeroAction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OnZeroActionId",
                table: "subactions_overlay_timer_start",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "subactions_overlay_timer_visibility",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CatchActionTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    TimerName = table.Column<string>(type: "text", nullable: false),
                    Visibility = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_overlay_timer_visibility", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_overlay_timer_visibility_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_overlay_timer_visibility_Actions_CatchActionType~",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subactions_overlay_timer_visibility_ActionTypeId",
                table: "subactions_overlay_timer_visibility",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_overlay_timer_visibility_CatchActionTypeId",
                table: "subactions_overlay_timer_visibility",
                column: "CatchActionTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subactions_overlay_timer_visibility");

            migrationBuilder.DropColumn(
                name: "OnZeroActionId",
                table: "subactions_overlay_timer_start");
        }
    }
}
