using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
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
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "subactions_overlay_timer_visibility",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActionTypeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CatchActionTypeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    TimerName = table.Column<string>(type: "TEXT", nullable: false),
                    Visibility = table.Column<string>(type: "TEXT", nullable: false)
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
                        name: "FK_subactions_overlay_timer_visibility_Actions_CatchActionTypeId",
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
