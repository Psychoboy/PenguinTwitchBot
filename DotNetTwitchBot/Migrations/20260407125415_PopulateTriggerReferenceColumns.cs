using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class PopulateTriggerReferenceColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Populate TimerGroupId from Configuration JSON for Timer triggers
            migrationBuilder.Sql(@"
                UPDATE triggers
                SET TimerGroupId = CAST(JSON_EXTRACT(Configuration, '$.TimerGroupId') AS UNSIGNED)
                WHERE Type = 2 
                  AND Configuration IS NOT NULL 
                  AND JSON_EXTRACT(Configuration, '$.TimerGroupId') IS NOT NULL;
            ");

            // Populate CommandId from Configuration JSON for Command triggers
            migrationBuilder.Sql(@"
                UPDATE triggers
                SET CommandId = CAST(JSON_EXTRACT(Configuration, '$.CommandId') AS UNSIGNED)
                WHERE Type = 0 
                  AND Configuration IS NOT NULL 
                  AND JSON_EXTRACT(Configuration, '$.CommandId') IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Clear the reference columns on downgrade
            migrationBuilder.Sql(@"
                UPDATE triggers
                SET TimerGroupId = NULL, CommandId = NULL;
            ");
        }
    }
}
