using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddBannedSongsAndOverlayTimer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BannedSongs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SongId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    BannedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BannedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BannedSongs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "subactions_overlay_timer_addtime",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_overlay_timer_addtime", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_overlay_timer_addtime_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_overlay_timer_addtime_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_overlay_timer_removetime",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_overlay_timer_removetime", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_overlay_timer_removetime_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_overlay_timer_removetime_Actions_CatchActionType~",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_overlay_timer_start",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    Direction = table.Column<string>(type: "text", nullable: false),
                    StartTime = table.Column<string>(type: "text", nullable: false),
                    ResetOnStart = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_overlay_timer_start", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_overlay_timer_start_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_overlay_timer_start_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_overlay_timer_stop",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    ResetOnStop = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_overlay_timer_stop", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_overlay_timer_stop_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_overlay_timer_stop_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BannedSongs_SongId",
                table: "BannedSongs",
                column: "SongId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subactions_overlay_timer_addtime_ActionTypeId",
                table: "subactions_overlay_timer_addtime",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_overlay_timer_addtime_CatchActionTypeId",
                table: "subactions_overlay_timer_addtime",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_overlay_timer_removetime_ActionTypeId",
                table: "subactions_overlay_timer_removetime",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_overlay_timer_removetime_CatchActionTypeId",
                table: "subactions_overlay_timer_removetime",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_overlay_timer_start_ActionTypeId",
                table: "subactions_overlay_timer_start",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_overlay_timer_start_CatchActionTypeId",
                table: "subactions_overlay_timer_start",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_overlay_timer_stop_ActionTypeId",
                table: "subactions_overlay_timer_stop",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_overlay_timer_stop_CatchActionTypeId",
                table: "subactions_overlay_timer_stop",
                column: "CatchActionTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BannedSongs");

            migrationBuilder.DropTable(
                name: "subactions_overlay_timer_addtime");

            migrationBuilder.DropTable(
                name: "subactions_overlay_timer_removetime");

            migrationBuilder.DropTable(
                name: "subactions_overlay_timer_start");

            migrationBuilder.DropTable(
                name: "subactions_overlay_timer_stop");
        }
    }
}
