using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddFireworksSubAction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subactions_fireworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CatchActionTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    Count = table.Column<string>(type: "text", nullable: false),
                    DelayMilliseconds = table.Column<string>(type: "text", nullable: false),
                    Shell = table.Column<string>(type: "text", nullable: false),
                    Size = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_fireworks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_fireworks_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_fireworks_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subactions_fireworks_ActionTypeId",
                table: "subactions_fireworks",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_fireworks_CatchActionTypeId",
                table: "subactions_fireworks",
                column: "CatchActionTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subactions_fireworks");
        }
    }
}
