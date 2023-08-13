using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class SongRequestMetricRanking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"CREATE VIEW `SongRequestMetricsWithRank` AS select songid, title, duration, requestedcount, ranking from ( select songid, title, duration, requestedcount, rank() over (order by requestedcount desc) as ranking from songrequestmetrics) t");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
