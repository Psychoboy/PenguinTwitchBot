using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class refactorRaffles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subactions_raffleend",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    RaffleKey = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_raffleend", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_raffleend_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_raffleenter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    RaffleKey = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_raffleenter", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_raffleenter_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_rafflegetentrycount",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    RaffleKey = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_rafflegetentrycount", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_rafflegetentrycount_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_rafflesettotalaward",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    RaffleKey = table.Column<string>(type: "text", nullable: false),
                    TotalAward = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_rafflesettotalaward", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_rafflesettotalaward_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_rafflesetwinnercount",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    RaffleKey = table.Column<string>(type: "text", nullable: false),
                    WinnerCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_rafflesetwinnercount", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_rafflesetwinnercount_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_rafflestart",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    RaffleKey = table.Column<string>(type: "text", nullable: false),
                    RaffleName = table.Column<string>(type: "text", nullable: false),
                    JoinCommand = table.Column<string>(type: "text", nullable: false),
                    PointGameName = table.Column<string>(type: "text", nullable: false),
                    WinnerCount = table.Column<int>(type: "integer", nullable: false),
                    TotalAward = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_rafflestart", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_rafflestart_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subactions_raffleend_ActionTypeId",
                table: "subactions_raffleend",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_raffleenter_ActionTypeId",
                table: "subactions_raffleenter",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_rafflegetentrycount_ActionTypeId",
                table: "subactions_rafflegetentrycount",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_rafflesettotalaward_ActionTypeId",
                table: "subactions_rafflesettotalaward",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_rafflesetwinnercount_ActionTypeId",
                table: "subactions_rafflesetwinnercount",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_rafflestart_ActionTypeId",
                table: "subactions_rafflestart",
                column: "ActionTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subactions_raffleend");

            migrationBuilder.DropTable(
                name: "subactions_raffleenter");

            migrationBuilder.DropTable(
                name: "subactions_rafflegetentrycount");

            migrationBuilder.DropTable(
                name: "subactions_rafflesettotalaward");

            migrationBuilder.DropTable(
                name: "subactions_rafflesetwinnercount");

            migrationBuilder.DropTable(
                name: "subactions_rafflestart");
        }
    }
}
