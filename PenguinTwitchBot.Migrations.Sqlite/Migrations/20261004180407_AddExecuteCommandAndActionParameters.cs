using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddExecuteCommandAndActionParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ElevatedCommand",
                table: "subactions_executeaction",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RankToExecuteAs",
                table: "subactions_executeaction",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "subactions_executecommand",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    CommandName = table.Column<string>(type: "TEXT", nullable: false),
                    ElevatedCommand = table.Column<bool>(type: "INTEGER", nullable: false),
                    RankToExecuteAs = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_executecommand", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_executecommand_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_executecommand_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subactions_executecommand_ActionTypeId",
                table: "subactions_executecommand",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_executecommand_CatchActionTypeId",
                table: "subactions_executecommand",
                column: "CatchActionTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subactions_executecommand");

            migrationBuilder.DropColumn(
                name: "ElevatedCommand",
                table: "subactions_executeaction");

            migrationBuilder.DropColumn(
                name: "RankToExecuteAs",
                table: "subactions_executeaction");
        }
    }
}
