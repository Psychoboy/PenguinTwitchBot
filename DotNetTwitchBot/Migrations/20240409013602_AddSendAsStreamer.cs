using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class AddSendAsStreamer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RespondAsStreamer",
                table: "CustomCommands",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RespondAsStreamer",
                table: "CustomCommands");
        }
    }
}
