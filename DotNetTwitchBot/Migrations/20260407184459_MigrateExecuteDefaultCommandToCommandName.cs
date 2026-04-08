using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class MigrateExecuteDefaultCommandToCommandName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add the new CommandName column (nullable initially)
            migrationBuilder.AddColumn<string>(
                name: "CommandName",
                table: "subactions_executedefaultcommand",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Step 2: Populate CommandName from DefaultCommands table using CommandId
            migrationBuilder.Sql(@"
                UPDATE subactions_executedefaultcommand edc
                INNER JOIN DefaultCommands dc ON edc.CommandId = dc.Id
                SET edc.CommandName = dc.CommandName
                WHERE edc.CommandId IS NOT NULL;
            ");

            // Step 3: For any records that couldn't be matched (orphaned data), set a default value
            // This prevents migration failure if there are orphaned records
            migrationBuilder.Sql(@"
                UPDATE subactions_executedefaultcommand
                SET CommandName = 'UNKNOWN'
                WHERE CommandName IS NULL;
            ");

            // Step 4: Make CommandName non-nullable now that it's populated
            migrationBuilder.AlterColumn<string>(
                name: "CommandName",
                table: "subactions_executedefaultcommand",
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
                table: "subactions_executedefaultcommand");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommandName",
                table: "subactions_executedefaultcommand");

            migrationBuilder.AddColumn<int>(
                name: "CommandId",
                table: "subactions_executedefaultcommand",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
