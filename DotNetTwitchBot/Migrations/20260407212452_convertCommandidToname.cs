using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class convertCommandidToname : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add CommandName column as nullable initially
            migrationBuilder.AddColumn<string>(
                name: "CommandName",
                table: "subactions_togglecommanddisabled",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Step 2: Populate CommandName from ActionCommands table using CommandId
            migrationBuilder.Sql(@"
                UPDATE subactions_togglecommanddisabled tcd
                INNER JOIN ActionCommands ac ON tcd.CommandId = ac.Id
                SET tcd.CommandName = ac.CommandName
                WHERE tcd.CommandId IS NOT NULL;
            ");

            // Step 3: Set a default value for any records that couldn't be matched
            migrationBuilder.Sql(@"
                UPDATE subactions_togglecommanddisabled
                SET CommandName = 'UNKNOWN'
                WHERE CommandName IS NULL;
            ");

            // Step 4: Make CommandName non-nullable now that all rows have values
            migrationBuilder.AlterColumn<string>(
                name: "CommandName",
                table: "subactions_togglecommanddisabled",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // Step 5: Drop the old CommandId column
            migrationBuilder.DropColumn(
                name: "CommandId",
                table: "subactions_togglecommanddisabled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommandName",
                table: "subactions_togglecommanddisabled");

            migrationBuilder.AddColumn<int>(
                name: "CommandId",
                table: "subactions_togglecommanddisabled",
                type: "int",
                nullable: true);
        }
    }
}
