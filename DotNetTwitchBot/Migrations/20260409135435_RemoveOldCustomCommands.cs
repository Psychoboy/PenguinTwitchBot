using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOldCustomCommands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomCommands");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomCommands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PointTypeId = table.Column<int>(type: "int", nullable: true),
                    Category = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CommandName = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Cost = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Disabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ExcludeFromUi = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    GlobalCooldown = table.Column<int>(type: "int", nullable: false),
                    MinimumRank = table.Column<int>(type: "int", nullable: false),
                    RespondAsStreamer = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Response = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SayCooldown = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SayRankRequirement = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SourceOnly = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SpecificRanks = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SpecificUserOnly = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SpecificUsersOnly = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserCooldown = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomCommands_PointTypes_PointTypeId",
                        column: x => x.PointTypeId,
                        principalTable: "PointTypes",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CustomCommands_CommandName",
                table: "CustomCommands",
                column: "CommandName");

            migrationBuilder.CreateIndex(
                name: "IX_CustomCommands_PointTypeId",
                table: "CustomCommands",
                column: "PointTypeId");
        }
    }
}
