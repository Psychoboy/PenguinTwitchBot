using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class addCostForCustomCommands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Cost",
                table: "CustomCommands",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cost",
                table: "CustomCommands");
        }
    }
}
