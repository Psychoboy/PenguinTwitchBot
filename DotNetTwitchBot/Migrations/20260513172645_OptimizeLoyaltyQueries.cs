using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeLoyaltyQueries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "ViewersTime",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "ViewerMessageCounts",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ViewersTime_Time",
                table: "ViewersTime",
                column: "Time");

            migrationBuilder.CreateIndex(
                name: "IX_ViewersTime_Username",
                table: "ViewersTime",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_ViewerMessageCounts_MessageCount",
                table: "ViewerMessageCounts",
                column: "MessageCount");

            migrationBuilder.CreateIndex(
                name: "IX_ViewerMessageCounts_Username",
                table: "ViewerMessageCounts",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_UserPoints_PointTypeId_Banned_Points",
                table: "UserPoints",
                columns: new[] { "PointTypeId", "Banned", "Points" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_UserPoints_UserId_PointTypeId",
                table: "UserPoints",
                columns: new[] { "UserId", "PointTypeId" });

            // Drop eliminated views
            migrationBuilder.Sql("DROP VIEW IF EXISTS `ViewersTimeWithRank`");

            migrationBuilder.Sql("DROP VIEW IF EXISTS `ViewerMessageCountWithRanks`");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ViewersTime_Time",
                table: "ViewersTime");

            migrationBuilder.DropIndex(
                name: "IX_ViewersTime_Username",
                table: "ViewersTime");

            migrationBuilder.DropIndex(
                name: "IX_ViewerMessageCounts_MessageCount",
                table: "ViewerMessageCounts");

            migrationBuilder.DropIndex(
                name: "IX_ViewerMessageCounts_Username",
                table: "ViewerMessageCounts");

            migrationBuilder.DropIndex(
                name: "IX_UserPoints_PointTypeId_Banned_Points",
                table: "UserPoints");

            migrationBuilder.DropIndex(
                name: "IX_UserPoints_UserId_PointTypeId",
                table: "UserPoints");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "ViewersTime",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "ViewerMessageCounts",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // Restore original RANK() OVER views
            migrationBuilder.Sql("DROP VIEW IF EXISTS `ViewersTimeWithRank`");
            migrationBuilder.Sql(@"CREATE VIEW `ViewersTimeWithRank` AS select id, username, Time, ranking from ( select id, username, Time, rank() over (order by Time desc) as ranking from ViewersTime) t ");

            migrationBuilder.Sql("DROP VIEW IF EXISTS `ViewerMessageCountWithRanks`");
            migrationBuilder.Sql(@"CREATE VIEW `ViewerMessageCountWithRanks` AS select id, username, MessageCount, ranking from ( select id, username, MessageCount, rank() over (order by MessageCount desc) as ranking from ViewerMessageCounts ) t ");
        }
    }
}
