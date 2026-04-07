using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class AddTriggerReferenceColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommandId",
                table: "Triggers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimerGroupId",
                table: "Triggers",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommandId",
                table: "Triggers");

            migrationBuilder.DropColumn(
                name: "TimerGroupId",
                table: "Triggers");
        }
    }
}
