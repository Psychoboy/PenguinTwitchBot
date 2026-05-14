using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class ConvertTriggersToOneToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionTriggers");

            migrationBuilder.AddColumn<int>(
                name: "ActionId",
                table: "Triggers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Triggers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Triggers_ActionId",
                table: "Triggers",
                column: "ActionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Triggers_Actions_ActionId",
                table: "Triggers",
                column: "ActionId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Triggers_Actions_ActionId",
                table: "Triggers");

            migrationBuilder.DropIndex(
                name: "IX_Triggers_ActionId",
                table: "Triggers");

            migrationBuilder.DropColumn(
                name: "ActionId",
                table: "Triggers");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Triggers");

            migrationBuilder.CreateTable(
                name: "ActionTriggers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ActionId = table.Column<int>(type: "int", nullable: false),
                    TriggerId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionTriggers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionTriggers_Actions_ActionId",
                        column: x => x.ActionId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActionTriggers_Triggers_TriggerId",
                        column: x => x.TriggerId,
                        principalTable: "Triggers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ActionTriggers_ActionId",
                table: "ActionTriggers",
                column: "ActionId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionTriggers_TriggerId",
                table: "ActionTriggers",
                column: "TriggerId");
        }
    }
}
