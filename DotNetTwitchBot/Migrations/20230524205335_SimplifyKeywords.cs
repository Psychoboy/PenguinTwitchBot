using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyKeywords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Keyword",
                table: "Keywords",
                newName: "CommandName");

            migrationBuilder.RenameColumn(
                name: "Cooldown",
                table: "Keywords",
                newName: "UserCooldown");

            migrationBuilder.AddColumn<int>(
                name: "Cost",
                table: "Keywords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Disabled",
                table: "Keywords",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "GlobalCooldown",
                table: "Keywords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinimumRank",
                table: "Keywords",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cost",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "Disabled",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "GlobalCooldown",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "MinimumRank",
                table: "Keywords");

            migrationBuilder.RenameColumn(
                name: "UserCooldown",
                table: "Keywords",
                newName: "Cooldown");

            migrationBuilder.RenameColumn(
                name: "CommandName",
                table: "Keywords",
                newName: "Keyword");
        }
    }
}
