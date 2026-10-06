using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddOpenAiSubAction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OpenAiResponseCodes",
                columns: table => new
                {
                    SessionKey = table.Column<string>(type: "TEXT", nullable: false),
                    PreviousResponseId = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenAiResponseCodes", x => x.SessionKey);
                });

            migrationBuilder.CreateTable(
                name: "subactions_openai",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Instructions = table.Column<string>(type: "TEXT", nullable: false),
                    Model = table.Column<string>(type: "TEXT", nullable: false),
                    MaxOutputTokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ServiceTier = table.Column<string>(type: "TEXT", nullable: false),
                    EnableWebSearch = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowedDomains = table.Column<string>(type: "TEXT", nullable: false),
                    SavePreviousResponse = table.Column<bool>(type: "INTEGER", nullable: false),
                    SessionKey = table.Column<string>(type: "TEXT", nullable: false),
                    ResponseVariableName = table.Column<string>(type: "TEXT", nullable: false),
                    CleanOutput = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_openai", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_openai_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_openai_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OpenAiResponseCodes_SessionKey",
                table: "OpenAiResponseCodes",
                column: "SessionKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subactions_openai_ActionTypeId",
                table: "subactions_openai",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_openai_CatchActionTypeId",
                table: "subactions_openai",
                column: "CatchActionTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OpenAiResponseCodes");

            migrationBuilder.DropTable(
                name: "subactions_openai");
        }
    }
}
