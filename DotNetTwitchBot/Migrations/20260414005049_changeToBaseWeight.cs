using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class changeToBaseWeight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxWeight",
                table: "FishTypes");

            migrationBuilder.RenameColumn(
                name: "MinWeight",
                table: "FishTypes",
                newName: "BaseWeight");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BaseWeight",
                table: "FishTypes",
                newName: "MinWeight");

            migrationBuilder.AddColumn<double>(
                name: "MaxWeight",
                table: "FishTypes",
                type: "double",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
