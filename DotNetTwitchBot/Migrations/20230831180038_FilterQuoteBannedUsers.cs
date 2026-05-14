using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class FilterQuoteBannedUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"create VIEW `FilteredQuotes` as SELECT  `Id`,`CreatedOn`,`CreatedBy`,`Game`,`QUOTE` FROM (SELECT  `Id`,`CreatedOn`,`CreatedBy`,`Game`,`QUOTE` FROM Quotes WHERE createdby NOT IN (SELECT username FROM BannedViewers)) t");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
