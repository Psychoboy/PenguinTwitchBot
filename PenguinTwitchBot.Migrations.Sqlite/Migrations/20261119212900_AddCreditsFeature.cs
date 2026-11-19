using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditsFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreditStreamSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    GameName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    IsManualCheckpoint = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditStreamSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CreditThemes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsBuiltIn = table.Column<bool>(type: "INTEGER", nullable: false),
                    BuiltInKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ThemeJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditThemes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "subactions_credits_add_entry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActionTypeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CatchActionTypeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    SectionTag = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<string>(type: "TEXT", nullable: false),
                    Detail = table.Column<string>(type: "TEXT", nullable: false),
                    AvatarUrl = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_credits_add_entry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_credits_add_entry_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_credits_add_entry_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_credits_mark_start",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActionTypeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CatchActionTypeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ProfileName = table.Column<string>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_credits_mark_start", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_credits_mark_start_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_credits_mark_start_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_credits_pause_resume",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActionTypeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CatchActionTypeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ProfileName = table.Column<string>(type: "TEXT", nullable: false),
                    ActionMode = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_credits_pause_resume", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_credits_pause_resume_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_credits_pause_resume_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_credits_play",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActionTypeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CatchActionTypeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ProfileName = table.Column<string>(type: "TEXT", nullable: false),
                    WindowOverrideMode = table.Column<string>(type: "TEXT", nullable: false),
                    LookbackMinutes = table.Column<string>(type: "TEXT", nullable: false),
                    SampleMode = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_credits_play", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_credits_play_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_credits_play_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_credits_stop",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActionTypeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CatchActionTypeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ProfileName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_credits_stop", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_credits_stop_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_credits_stop_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    AvatarUrl = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Tier = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StreamSessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsAnonymous = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsManual = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsHidden = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditEvents_CreditStreamSessions_StreamSessionId",
                        column: x => x.StreamSessionId,
                        principalTable: "CreditStreamSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CreditProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    ThemeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditProfiles_CreditThemes_ThemeId",
                        column: x => x.ThemeId,
                        principalTable: "CreditThemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditEvents_EventType_OccurredAtUtc",
                table: "CreditEvents",
                columns: new[] { "EventType", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditEvents_OccurredAtUtc",
                table: "CreditEvents",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CreditEvents_StreamSessionId_EventType",
                table: "CreditEvents",
                columns: new[] { "StreamSessionId", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditEvents_UserId_OccurredAtUtc",
                table: "CreditEvents",
                columns: new[] { "UserId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditProfiles_Name",
                table: "CreditProfiles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditProfiles_ThemeId",
                table: "CreditProfiles",
                column: "ThemeId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditStreamSessions_StartedAtUtc",
                table: "CreditStreamSessions",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CreditThemes_BuiltInKey",
                table: "CreditThemes",
                column: "BuiltInKey");

            migrationBuilder.CreateIndex(
                name: "IX_CreditThemes_Name",
                table: "CreditThemes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subactions_credits_add_entry_ActionTypeId",
                table: "subactions_credits_add_entry",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_credits_add_entry_CatchActionTypeId",
                table: "subactions_credits_add_entry",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_credits_mark_start_ActionTypeId",
                table: "subactions_credits_mark_start",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_credits_mark_start_CatchActionTypeId",
                table: "subactions_credits_mark_start",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_credits_pause_resume_ActionTypeId",
                table: "subactions_credits_pause_resume",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_credits_pause_resume_CatchActionTypeId",
                table: "subactions_credits_pause_resume",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_credits_play_ActionTypeId",
                table: "subactions_credits_play",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_credits_play_CatchActionTypeId",
                table: "subactions_credits_play",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_credits_stop_ActionTypeId",
                table: "subactions_credits_stop",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_credits_stop_CatchActionTypeId",
                table: "subactions_credits_stop",
                column: "CatchActionTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditEvents");

            migrationBuilder.DropTable(
                name: "CreditProfiles");

            migrationBuilder.DropTable(
                name: "subactions_credits_add_entry");

            migrationBuilder.DropTable(
                name: "subactions_credits_mark_start");

            migrationBuilder.DropTable(
                name: "subactions_credits_pause_resume");

            migrationBuilder.DropTable(
                name: "subactions_credits_play");

            migrationBuilder.DropTable(
                name: "subactions_credits_stop");

            migrationBuilder.DropTable(
                name: "CreditStreamSessions");

            migrationBuilder.DropTable(
                name: "CreditThemes");
        }
    }
}
