using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class ReworkCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Counters_CounterName",
                table: "Counters");

            migrationBuilder.AddColumn<string>(
                name: "DestinationVariable",
                table: "subactions_multicounter",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Operation",
                table: "subactions_multicounter",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Value",
                table: "subactions_multicounter",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DecrementRank",
                table: "Counters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Counters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IncrementRank",
                table: "Counters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InitialValue",
                table: "Counters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Max",
                table: "Counters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Min",
                table: "Counters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResetRank",
                table: "Counters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SetRank",
                table: "Counters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Step",
                table: "Counters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Counters_CounterName",
                table: "Counters",
                column: "CounterName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Counters_CounterName",
                table: "Counters");

            migrationBuilder.DropColumn(
                name: "DestinationVariable",
                table: "subactions_multicounter");

            migrationBuilder.DropColumn(
                name: "Operation",
                table: "subactions_multicounter");

            migrationBuilder.DropColumn(
                name: "Value",
                table: "subactions_multicounter");

            migrationBuilder.DropColumn(
                name: "DecrementRank",
                table: "Counters");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Counters");

            migrationBuilder.DropColumn(
                name: "IncrementRank",
                table: "Counters");

            migrationBuilder.DropColumn(
                name: "InitialValue",
                table: "Counters");

            migrationBuilder.DropColumn(
                name: "Max",
                table: "Counters");

            migrationBuilder.DropColumn(
                name: "Min",
                table: "Counters");

            migrationBuilder.DropColumn(
                name: "ResetRank",
                table: "Counters");

            migrationBuilder.DropColumn(
                name: "SetRank",
                table: "Counters");

            migrationBuilder.DropColumn(
                name: "Step",
                table: "Counters");

            migrationBuilder.CreateIndex(
                name: "IX_Counters_CounterName",
                table: "Counters",
                column: "CounterName");
        }
    }
}
