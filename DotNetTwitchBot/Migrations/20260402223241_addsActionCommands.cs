using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class addsActionCommands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActionCommands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CommandName = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserCooldown = table.Column<int>(type: "int", nullable: false),
                    GlobalCooldown = table.Column<int>(type: "int", nullable: false),
                    MinimumRank = table.Column<int>(type: "int", nullable: false),
                    Cost = table.Column<int>(type: "int", nullable: false),
                    PointTypeId = table.Column<int>(type: "int", nullable: true),
                    Disabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SayCooldown = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SayRankRequirement = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ExcludeFromUi = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SourceOnly = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Category = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RunFromBroadcasterOnly = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SpecificUserOnly = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SpecificUsersOnly = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SpecificRanks = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionCommands_PointTypes_PointTypeId",
                        column: x => x.PointTypeId,
                        principalTable: "PointTypes",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ActionCommands_CommandName",
                table: "ActionCommands",
                column: "CommandName");

            migrationBuilder.CreateIndex(
                name: "IX_ActionCommands_PointTypeId",
                table: "ActionCommands",
                column: "PointTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionCommands");
        }
    }
}
