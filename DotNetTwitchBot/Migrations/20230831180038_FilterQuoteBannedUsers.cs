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
            migrationBuilder.Sql(@"create VIEW `filteredquotes` as SELECT  `Id`,`CreatedOn`,`CreatedBy`,`Game`,`QUOTE` FROM (SELECT  `Id`,`CreatedOn`,`CreatedBy`,`Game`,`QUOTE` FROM quotes WHERE createdby NOT IN (SELECT username FROM bannedviewers)) t");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
