using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class AddPointTypeToCommands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PointTypeId",
                table: "Keywords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PointTypeId",
                table: "ExternalCommands",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PointTypeId",
                table: "DefaultCommands",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PointTypeId",
                table: "CustomCommands",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PointTypeId",
                table: "AudioCommands",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Keywords_PointTypeId",
                table: "Keywords",
                column: "PointTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalCommands_PointTypeId",
                table: "ExternalCommands",
                column: "PointTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DefaultCommands_PointTypeId",
                table: "DefaultCommands",
                column: "PointTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomCommands_PointTypeId",
                table: "CustomCommands",
                column: "PointTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AudioCommands_PointTypeId",
                table: "AudioCommands",
                column: "PointTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AudioCommands_PointTypes_PointTypeId",
                table: "AudioCommands",
                column: "PointTypeId",
                principalTable: "PointTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomCommands_PointTypes_PointTypeId",
                table: "CustomCommands",
                column: "PointTypeId",
                principalTable: "PointTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DefaultCommands_PointTypes_PointTypeId",
                table: "DefaultCommands",
                column: "PointTypeId",
                principalTable: "PointTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalCommands_PointTypes_PointTypeId",
                table: "ExternalCommands",
                column: "PointTypeId",
                principalTable: "PointTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Keywords_PointTypes_PointTypeId",
                table: "Keywords",
                column: "PointTypeId",
                principalTable: "PointTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AudioCommands_PointTypes_PointTypeId",
                table: "AudioCommands");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomCommands_PointTypes_PointTypeId",
                table: "CustomCommands");

            migrationBuilder.DropForeignKey(
                name: "FK_DefaultCommands_PointTypes_PointTypeId",
                table: "DefaultCommands");

            migrationBuilder.DropForeignKey(
                name: "FK_ExternalCommands_PointTypes_PointTypeId",
                table: "ExternalCommands");

            migrationBuilder.DropForeignKey(
                name: "FK_Keywords_PointTypes_PointTypeId",
                table: "Keywords");

            migrationBuilder.DropIndex(
                name: "IX_Keywords_PointTypeId",
                table: "Keywords");

            migrationBuilder.DropIndex(
                name: "IX_ExternalCommands_PointTypeId",
                table: "ExternalCommands");

            migrationBuilder.DropIndex(
                name: "IX_DefaultCommands_PointTypeId",
                table: "DefaultCommands");

            migrationBuilder.DropIndex(
                name: "IX_CustomCommands_PointTypeId",
                table: "CustomCommands");

            migrationBuilder.DropIndex(
                name: "IX_AudioCommands_PointTypeId",
                table: "AudioCommands");

            migrationBuilder.DropColumn(
                name: "PointTypeId",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "PointTypeId",
                table: "ExternalCommands");

            migrationBuilder.DropColumn(
                name: "PointTypeId",
                table: "DefaultCommands");

            migrationBuilder.DropColumn(
                name: "PointTypeId",
                table: "CustomCommands");

            migrationBuilder.DropColumn(
                name: "PointTypeId",
                table: "AudioCommands");
        }
    }
}
