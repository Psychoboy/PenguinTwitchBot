using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class AddActionSubActionRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubActions_Actions_ActionTypeId",
                table: "SubActions");

            migrationBuilder.AddForeignKey(
                name: "FK_SubActions_Actions_ActionTypeId",
                table: "SubActions",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubActions_Actions_ActionTypeId",
                table: "SubActions");

            migrationBuilder.AddForeignKey(
                name: "FK_SubActions_Actions_ActionTypeId",
                table: "SubActions",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id");
        }
    }
}
