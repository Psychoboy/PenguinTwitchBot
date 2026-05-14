using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class updateTimersToMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IntervalMinimumSeconds",
                table: "TimerGroups",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IntervalMaximumSeconds",
                table: "TimerGroups",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
                UPDATE TimerGroups 
                SET IntervalMinimumSeconds = IntervalMinimum * 60,
                    IntervalMaximumSeconds = IntervalMaximum * 60");

            migrationBuilder.DropColumn(
                name: "IntervalMinimum",
                table: "TimerGroups");

            migrationBuilder.DropColumn(
                name: "IntervalMaximum",
                table: "TimerGroups");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IntervalMinimum",
                table: "TimerGroups",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IntervalMaximum",
                table: "TimerGroups",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
                UPDATE TimerGroups 
                SET IntervalMinimum = IntervalMinimumSeconds / 60,
                    IntervalMaximum = IntervalMaximumSeconds / 60");

            migrationBuilder.DropColumn(
                name: "IntervalMinimumSeconds",
                table: "TimerGroups");

            migrationBuilder.DropColumn(
                name: "IntervalMaximumSeconds",
                table: "TimerGroups");
        }
    }
}
