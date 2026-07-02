using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class ChatHistoryRetentionAndRemoveMarkov : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarkovValues");

            migrationBuilder.Sql("DELETE FROM \"DefaultCommands\" WHERE lower(\"ModuleName\") = 'markovchat' OR lower(\"CommandName\") = 'g';");
            migrationBuilder.Sql("DELETE FROM \"GameSettings\" WHERE lower(\"GameName\") = 'markovchat';");

            migrationBuilder.CreateIndex(
                name: "IX_ViewerChatHistories_CreatedAt",
                table: "ViewerChatHistories",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ViewerChatHistories_MessageId",
                table: "ViewerChatHistories",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ViewerChatHistories_Username_CreatedAt",
                table: "ViewerChatHistories",
                columns: new[] { "Username", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ViewerChatHistories_CreatedAt",
                table: "ViewerChatHistories");

            migrationBuilder.DropIndex(
                name: "IX_ViewerChatHistories_MessageId",
                table: "ViewerChatHistories");

            migrationBuilder.DropIndex(
                name: "IX_ViewerChatHistories_Username_CreatedAt",
                table: "ViewerChatHistories");

            migrationBuilder.CreateTable(
                name: "MarkovValues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KeyIndex = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarkovValues", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarkovValues_KeyIndex",
                table: "MarkovValues",
                column: "KeyIndex");
        }
    }
}
