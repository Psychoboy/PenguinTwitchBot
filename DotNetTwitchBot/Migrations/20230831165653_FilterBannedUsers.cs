using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class FilterBannedUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"alter  VIEW `viewerticketwithranks` AS select id, username, points, ranking from ( select id, username, points, rank() over (order by Points desc) as ranking from viewertickets WHERE username NOT IN (SELECT username FROM bannedviewers)) t ");
            migrationBuilder.Sql(@"alter  VIEW `ViewerMessageCountWithRanks` AS select id, username, MessageCount, ranking from ( select id, username, MessageCount, rank() over (order by MessageCount desc) as ranking from ViewerMessageCounts WHERE username NOT IN (SELECT username FROM bannedviewers)) t ");
            migrationBuilder.Sql(@"alter  VIEW `viewerpointwithranks` AS select id, username, points, ranking from ( select id, username, points, rank() over (order by Points desc) as ranking from viewerpoints WHERE username NOT IN (SELECT username FROM bannedviewers)) t ");
            migrationBuilder.Sql(@"alter  VIEW `ViewersTimeWithRank` AS select id, username, Time, ranking from ( select id, username, Time, rank() over (order by Time desc) as ranking from ViewersTime WHERE username NOT IN (SELECT username FROM bannedviewers)) t ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
