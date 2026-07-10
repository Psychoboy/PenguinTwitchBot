using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalVariables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GlobalVariables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalVariables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "subactions_getglobalvariable",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetVariableName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_getglobalvariable", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_getglobalvariable_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_getglobalvariable_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_setglobalvariable",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_setglobalvariable", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_setglobalvariable_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_setglobalvariable_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GlobalVariables_Name",
                table: "GlobalVariables",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subactions_getglobalvariable_ActionTypeId",
                table: "subactions_getglobalvariable",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_getglobalvariable_CatchActionTypeId",
                table: "subactions_getglobalvariable",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_setglobalvariable_ActionTypeId",
                table: "subactions_setglobalvariable",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_setglobalvariable_CatchActionTypeId",
                table: "subactions_setglobalvariable",
                column: "CatchActionTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GlobalVariables");

            migrationBuilder.DropTable(
                name: "subactions_getglobalvariable");

            migrationBuilder.DropTable(
                name: "subactions_setglobalvariable");
        }
    }
}
