using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class addActionKeywords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KeywordId",
                table: "Triggers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActionKeywords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Response = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsRegex = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsCaseSensitive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CommandName = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserCooldown = table.Column<int>(type: "int", nullable: false),
                    UserCooldownMax = table.Column<int>(type: "int", nullable: false),
                    GlobalCooldown = table.Column<int>(type: "int", nullable: false),
                    GlobalCooldownMax = table.Column<int>(type: "int", nullable: false),
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
                    SpecificUserOnly = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SpecificUsersOnly = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SpecificRanks = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionKeywords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionKeywords_PointTypes_PointTypeId",
                        column: x => x.PointTypeId,
                        principalTable: "PointTypes",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ActionKeywords_CommandName",
                table: "ActionKeywords",
                column: "CommandName");

            migrationBuilder.CreateIndex(
                name: "IX_ActionKeywords_PointTypeId",
                table: "ActionKeywords",
                column: "PointTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionKeywords");

            migrationBuilder.DropColumn(
                name: "KeywordId",
                table: "Triggers");
        }
    }
}
