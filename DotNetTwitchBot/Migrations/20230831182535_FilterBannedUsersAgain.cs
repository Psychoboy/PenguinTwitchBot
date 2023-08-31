using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class FilterBannedUsersAgain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "banned",
                table: "ViewerTickets",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "banned",
                table: "ViewersTime",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "banned",
                table: "ViewerPoints",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "banned",
                table: "ViewerMessageCounts",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(@"alter  VIEW `viewerticketwithranks` AS select id, username, points, ranking from ( select id, username, points, rank() over (order by Points desc) as ranking from viewertickets WHERE banned = false) t ");
            migrationBuilder.Sql(@"alter  VIEW `ViewerMessageCountWithRanks` AS select id, username, MessageCount, ranking from ( select id, username, MessageCount, rank() over (order by MessageCount desc) as ranking from ViewerMessageCounts WHERE banned = false) t ");
            migrationBuilder.Sql(@"alter  VIEW `viewerpointwithranks` AS select id, username, points, ranking from ( select id, username, points, rank() over (order by Points desc) as ranking from viewerpoints WHERE  banned = false) t ");
            migrationBuilder.Sql(@"alter  VIEW `ViewersTimeWithRank` AS select id, username, Time, ranking from ( select id, username, Time, rank() over (order by Time desc) as ranking from ViewersTime WHERE  banned = false) t ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "banned",
                table: "ViewerTickets");

            migrationBuilder.DropColumn(
                name: "banned",
                table: "ViewersTime");

            migrationBuilder.DropColumn(
                name: "banned",
                table: "ViewerPoints");

            migrationBuilder.DropColumn(
                name: "banned",
                table: "ViewerMessageCounts");
        }
    }
}
