using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class refactorLoyaltyPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subactions_foreachviewer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    ActionId = table.Column<int>(type: "INTEGER", nullable: true),
                    ActionName = table.Column<string>(type: "TEXT", nullable: false),
                    ViewerScope = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_foreachviewer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_foreachviewer_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subactions_foreachviewer_ActionTypeId",
                table: "subactions_foreachviewer",
                column: "ActionTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subactions_foreachviewer");
        }
    }
}
