using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultCommandsToTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddActiveCommand",
                table: "PointTypes");

            migrationBuilder.DropColumn(
                name: "AddCommand",
                table: "PointTypes");

            migrationBuilder.DropColumn(
                name: "GetCommand",
                table: "PointTypes");

            migrationBuilder.DropColumn(
                name: "RemoveCommand",
                table: "PointTypes");

            migrationBuilder.DropColumn(
                name: "SetCommand",
                table: "PointTypes");

            migrationBuilder.CreateTable(
                name: "PointCommands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PointTypeId = table.Column<int>(type: "int", nullable: false),
                    CommandType = table.Column<int>(type: "int", nullable: false),
                    CommandName = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserCooldown = table.Column<int>(type: "int", nullable: false),
                    GlobalCooldown = table.Column<int>(type: "int", nullable: false),
                    MinimumRank = table.Column<int>(type: "int", nullable: false),
                    Cost = table.Column<int>(type: "int", nullable: false),
                    Disabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SayCooldown = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SayRankRequirement = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ExcludeFromUi = table.Column<bool>(type: "tinyint(1)", nullable: false),
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
                    table.PrimaryKey("PK_PointCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PointCommands_PointTypes_PointTypeId",
                        column: x => x.PointTypeId,
                        principalTable: "PointTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PointCommands_CommandName",
                table: "PointCommands",
                column: "CommandName");

            migrationBuilder.CreateIndex(
                name: "IX_PointCommands_PointTypeId",
                table: "PointCommands",
                column: "PointTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PointCommands");

            migrationBuilder.AddColumn<string>(
                name: "AddActiveCommand",
                table: "PointTypes",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AddCommand",
                table: "PointTypes",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "GetCommand",
                table: "PointTypes",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RemoveCommand",
                table: "PointTypes",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SetCommand",
                table: "PointTypes",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
