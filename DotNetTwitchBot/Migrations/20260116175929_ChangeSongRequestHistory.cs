using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class ChangeSongRequestHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW `songrequesthistorywithrank`");
            migrationBuilder.Sql("CREATE VIEW `SongRequestHistoryWithRank` AS select songid, title, duration, requestedcount, ranking, LastRequestDate from(select songid, title, duration, COUNT(SongId) AS requestedCount, rank() over (order by requestedcount desc) as ranking,MAX(RequestDate) AS LastRequestDate from SongRequestHistories GROUP BY SongId) t");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW `songrequesthistorywithrank`");
            migrationBuilder.Sql("CREATE VIEW `SongRequestHistoryWithRank` AS select songid, title, duration, requestedcount, ranking, LastRequestDate from(select songid, title, duration, COUNT(SongId) AS requestedCount, rank() over (order by requestedcount desc) as ranking,MAX(RequestDate) AS LastRequestDate from SongRequestHistories WHERE RequestDate >= (CURDATE() - INTERVAL 1 MONTH) GROUP BY SongId) t");
        }
    }
}
