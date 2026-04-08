using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class FixTableNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlertType_Actions_ActionTypeId",
                table: "AlertType");

            migrationBuilder.DropForeignKey(
                name: "FK_CurrentTimeType_Actions_ActionTypeId",
                table: "CurrentTimeType");

            migrationBuilder.DropForeignKey(
                name: "FK_ExternalApiType_Actions_ActionTypeId",
                table: "ExternalApiType");

            migrationBuilder.DropForeignKey(
                name: "FK_FollowAgeType_Actions_ActionTypeId",
                table: "FollowAgeType");

            migrationBuilder.DropForeignKey(
                name: "FK_GiveawayPrizeType_Actions_ActionTypeId",
                table: "GiveawayPrizeType");

            migrationBuilder.DropForeignKey(
                name: "FK_MultiCounterType_Actions_ActionTypeId",
                table: "MultiCounterType");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaySoundType_Actions_ActionTypeId",
                table: "PlaySoundType");

            migrationBuilder.DropForeignKey(
                name: "FK_RandomIntType_Actions_ActionTypeId",
                table: "RandomIntType");

            migrationBuilder.DropForeignKey(
                name: "FK_ReplyToMessageType_Actions_ActionTypeId",
                table: "ReplyToMessageType");

            migrationBuilder.DropForeignKey(
                name: "FK_SendMessageType_Actions_ActionTypeId",
                table: "SendMessageType");

            migrationBuilder.DropForeignKey(
                name: "FK_UptimeType_Actions_ActionTypeId",
                table: "UptimeType");

            migrationBuilder.DropForeignKey(
                name: "FK_WatchTimeType_Actions_ActionTypeId",
                table: "WatchTimeType");

            migrationBuilder.DropForeignKey(
                name: "FK_WriteFileType_Actions_ActionTypeId",
                table: "WriteFileType");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WriteFileType",
                table: "WriteFileType");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WatchTimeType",
                table: "WatchTimeType");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UptimeType",
                table: "UptimeType");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SendMessageType",
                table: "SendMessageType");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ReplyToMessageType",
                table: "ReplyToMessageType");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RandomIntType",
                table: "RandomIntType");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlaySoundType",
                table: "PlaySoundType");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MultiCounterType",
                table: "MultiCounterType");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GiveawayPrizeType",
                table: "GiveawayPrizeType");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FollowAgeType",
                table: "FollowAgeType");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ExternalApiType",
                table: "ExternalApiType");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CurrentTimeType",
                table: "CurrentTimeType");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AlertType",
                table: "AlertType");

            migrationBuilder.RenameTable(
                name: "WriteFileType",
                newName: "subactions_writefile");

            migrationBuilder.RenameTable(
                name: "WatchTimeType",
                newName: "subactions_watchtime");

            migrationBuilder.RenameTable(
                name: "UptimeType",
                newName: "subactions_uptime");

            migrationBuilder.RenameTable(
                name: "SendMessageType",
                newName: "subactions_sendmessage");

            migrationBuilder.RenameTable(
                name: "ReplyToMessageType",
                newName: "subactions_replytomessage");

            migrationBuilder.RenameTable(
                name: "RandomIntType",
                newName: "subactions_randomint");

            migrationBuilder.RenameTable(
                name: "PlaySoundType",
                newName: "subactions_playsound");

            migrationBuilder.RenameTable(
                name: "MultiCounterType",
                newName: "subactions_multicounter");

            migrationBuilder.RenameTable(
                name: "GiveawayPrizeType",
                newName: "subactions_giveawayprize");

            migrationBuilder.RenameTable(
                name: "FollowAgeType",
                newName: "subactions_followage");

            migrationBuilder.RenameTable(
                name: "ExternalApiType",
                newName: "subactions_externalapi");

            migrationBuilder.RenameTable(
                name: "CurrentTimeType",
                newName: "subactions_currenttime");

            migrationBuilder.RenameTable(
                name: "AlertType",
                newName: "subactions_alert");

            migrationBuilder.RenameIndex(
                name: "IX_WriteFileType_ActionTypeId",
                table: "subactions_writefile",
                newName: "IX_subactions_writefile_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_WatchTimeType_ActionTypeId",
                table: "subactions_watchtime",
                newName: "IX_subactions_watchtime_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_UptimeType_ActionTypeId",
                table: "subactions_uptime",
                newName: "IX_subactions_uptime_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_SendMessageType_ActionTypeId",
                table: "subactions_sendmessage",
                newName: "IX_subactions_sendmessage_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_ReplyToMessageType_ActionTypeId",
                table: "subactions_replytomessage",
                newName: "IX_subactions_replytomessage_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_RandomIntType_ActionTypeId",
                table: "subactions_randomint",
                newName: "IX_subactions_randomint_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_PlaySoundType_ActionTypeId",
                table: "subactions_playsound",
                newName: "IX_subactions_playsound_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_MultiCounterType_ActionTypeId",
                table: "subactions_multicounter",
                newName: "IX_subactions_multicounter_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_GiveawayPrizeType_ActionTypeId",
                table: "subactions_giveawayprize",
                newName: "IX_subactions_giveawayprize_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_FollowAgeType_ActionTypeId",
                table: "subactions_followage",
                newName: "IX_subactions_followage_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_ExternalApiType_ActionTypeId",
                table: "subactions_externalapi",
                newName: "IX_subactions_externalapi_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_CurrentTimeType_ActionTypeId",
                table: "subactions_currenttime",
                newName: "IX_subactions_currenttime_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_AlertType_ActionTypeId",
                table: "subactions_alert",
                newName: "IX_subactions_alert_ActionTypeId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_subactions_writefile",
                table: "subactions_writefile",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_subactions_watchtime",
                table: "subactions_watchtime",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_subactions_uptime",
                table: "subactions_uptime",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_subactions_sendmessage",
                table: "subactions_sendmessage",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_subactions_replytomessage",
                table: "subactions_replytomessage",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_subactions_randomint",
                table: "subactions_randomint",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_subactions_playsound",
                table: "subactions_playsound",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_subactions_multicounter",
                table: "subactions_multicounter",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_subactions_giveawayprize",
                table: "subactions_giveawayprize",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_subactions_followage",
                table: "subactions_followage",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_subactions_externalapi",
                table: "subactions_externalapi",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_subactions_currenttime",
                table: "subactions_currenttime",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_subactions_alert",
                table: "subactions_alert",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_alert_Actions_ActionTypeId",
                table: "subactions_alert",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_currenttime_Actions_ActionTypeId",
                table: "subactions_currenttime",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_externalapi_Actions_ActionTypeId",
                table: "subactions_externalapi",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_followage_Actions_ActionTypeId",
                table: "subactions_followage",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_giveawayprize_Actions_ActionTypeId",
                table: "subactions_giveawayprize",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_multicounter_Actions_ActionTypeId",
                table: "subactions_multicounter",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_playsound_Actions_ActionTypeId",
                table: "subactions_playsound",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_randomint_Actions_ActionTypeId",
                table: "subactions_randomint",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_replytomessage_Actions_ActionTypeId",
                table: "subactions_replytomessage",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_sendmessage_Actions_ActionTypeId",
                table: "subactions_sendmessage",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_uptime_Actions_ActionTypeId",
                table: "subactions_uptime",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_watchtime_Actions_ActionTypeId",
                table: "subactions_watchtime",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_writefile_Actions_ActionTypeId",
                table: "subactions_writefile",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_subactions_alert_Actions_ActionTypeId",
                table: "subactions_alert");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_currenttime_Actions_ActionTypeId",
                table: "subactions_currenttime");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_externalapi_Actions_ActionTypeId",
                table: "subactions_externalapi");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_followage_Actions_ActionTypeId",
                table: "subactions_followage");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_giveawayprize_Actions_ActionTypeId",
                table: "subactions_giveawayprize");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_multicounter_Actions_ActionTypeId",
                table: "subactions_multicounter");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_playsound_Actions_ActionTypeId",
                table: "subactions_playsound");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_randomint_Actions_ActionTypeId",
                table: "subactions_randomint");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_replytomessage_Actions_ActionTypeId",
                table: "subactions_replytomessage");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_sendmessage_Actions_ActionTypeId",
                table: "subactions_sendmessage");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_uptime_Actions_ActionTypeId",
                table: "subactions_uptime");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_watchtime_Actions_ActionTypeId",
                table: "subactions_watchtime");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_writefile_Actions_ActionTypeId",
                table: "subactions_writefile");

            migrationBuilder.DropPrimaryKey(
                name: "PK_subactions_writefile",
                table: "subactions_writefile");

            migrationBuilder.DropPrimaryKey(
                name: "PK_subactions_watchtime",
                table: "subactions_watchtime");

            migrationBuilder.DropPrimaryKey(
                name: "PK_subactions_uptime",
                table: "subactions_uptime");

            migrationBuilder.DropPrimaryKey(
                name: "PK_subactions_sendmessage",
                table: "subactions_sendmessage");

            migrationBuilder.DropPrimaryKey(
                name: "PK_subactions_replytomessage",
                table: "subactions_replytomessage");

            migrationBuilder.DropPrimaryKey(
                name: "PK_subactions_randomint",
                table: "subactions_randomint");

            migrationBuilder.DropPrimaryKey(
                name: "PK_subactions_playsound",
                table: "subactions_playsound");

            migrationBuilder.DropPrimaryKey(
                name: "PK_subactions_multicounter",
                table: "subactions_multicounter");

            migrationBuilder.DropPrimaryKey(
                name: "PK_subactions_giveawayprize",
                table: "subactions_giveawayprize");

            migrationBuilder.DropPrimaryKey(
                name: "PK_subactions_followage",
                table: "subactions_followage");

            migrationBuilder.DropPrimaryKey(
                name: "PK_subactions_externalapi",
                table: "subactions_externalapi");

            migrationBuilder.DropPrimaryKey(
                name: "PK_subactions_currenttime",
                table: "subactions_currenttime");

            migrationBuilder.DropPrimaryKey(
                name: "PK_subactions_alert",
                table: "subactions_alert");

            migrationBuilder.RenameTable(
                name: "subactions_writefile",
                newName: "WriteFileType");

            migrationBuilder.RenameTable(
                name: "subactions_watchtime",
                newName: "WatchTimeType");

            migrationBuilder.RenameTable(
                name: "subactions_uptime",
                newName: "UptimeType");

            migrationBuilder.RenameTable(
                name: "subactions_sendmessage",
                newName: "SendMessageType");

            migrationBuilder.RenameTable(
                name: "subactions_replytomessage",
                newName: "ReplyToMessageType");

            migrationBuilder.RenameTable(
                name: "subactions_randomint",
                newName: "RandomIntType");

            migrationBuilder.RenameTable(
                name: "subactions_playsound",
                newName: "PlaySoundType");

            migrationBuilder.RenameTable(
                name: "subactions_multicounter",
                newName: "MultiCounterType");

            migrationBuilder.RenameTable(
                name: "subactions_giveawayprize",
                newName: "GiveawayPrizeType");

            migrationBuilder.RenameTable(
                name: "subactions_followage",
                newName: "FollowAgeType");

            migrationBuilder.RenameTable(
                name: "subactions_externalapi",
                newName: "ExternalApiType");

            migrationBuilder.RenameTable(
                name: "subactions_currenttime",
                newName: "CurrentTimeType");

            migrationBuilder.RenameTable(
                name: "subactions_alert",
                newName: "AlertType");

            migrationBuilder.RenameIndex(
                name: "IX_subactions_writefile_ActionTypeId",
                table: "WriteFileType",
                newName: "IX_WriteFileType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_subactions_watchtime_ActionTypeId",
                table: "WatchTimeType",
                newName: "IX_WatchTimeType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_subactions_uptime_ActionTypeId",
                table: "UptimeType",
                newName: "IX_UptimeType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_subactions_sendmessage_ActionTypeId",
                table: "SendMessageType",
                newName: "IX_SendMessageType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_subactions_replytomessage_ActionTypeId",
                table: "ReplyToMessageType",
                newName: "IX_ReplyToMessageType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_subactions_randomint_ActionTypeId",
                table: "RandomIntType",
                newName: "IX_RandomIntType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_subactions_playsound_ActionTypeId",
                table: "PlaySoundType",
                newName: "IX_PlaySoundType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_subactions_multicounter_ActionTypeId",
                table: "MultiCounterType",
                newName: "IX_MultiCounterType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_subactions_giveawayprize_ActionTypeId",
                table: "GiveawayPrizeType",
                newName: "IX_GiveawayPrizeType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_subactions_followage_ActionTypeId",
                table: "FollowAgeType",
                newName: "IX_FollowAgeType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_subactions_externalapi_ActionTypeId",
                table: "ExternalApiType",
                newName: "IX_ExternalApiType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_subactions_currenttime_ActionTypeId",
                table: "CurrentTimeType",
                newName: "IX_CurrentTimeType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_subactions_alert_ActionTypeId",
                table: "AlertType",
                newName: "IX_AlertType_ActionTypeId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WriteFileType",
                table: "WriteFileType",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WatchTimeType",
                table: "WatchTimeType",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UptimeType",
                table: "UptimeType",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SendMessageType",
                table: "SendMessageType",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ReplyToMessageType",
                table: "ReplyToMessageType",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RandomIntType",
                table: "RandomIntType",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlaySoundType",
                table: "PlaySoundType",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MultiCounterType",
                table: "MultiCounterType",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GiveawayPrizeType",
                table: "GiveawayPrizeType",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FollowAgeType",
                table: "FollowAgeType",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ExternalApiType",
                table: "ExternalApiType",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CurrentTimeType",
                table: "CurrentTimeType",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AlertType",
                table: "AlertType",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AlertType_Actions_ActionTypeId",
                table: "AlertType",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CurrentTimeType_Actions_ActionTypeId",
                table: "CurrentTimeType",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalApiType_Actions_ActionTypeId",
                table: "ExternalApiType",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FollowAgeType_Actions_ActionTypeId",
                table: "FollowAgeType",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GiveawayPrizeType_Actions_ActionTypeId",
                table: "GiveawayPrizeType",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MultiCounterType_Actions_ActionTypeId",
                table: "MultiCounterType",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaySoundType_Actions_ActionTypeId",
                table: "PlaySoundType",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RandomIntType_Actions_ActionTypeId",
                table: "RandomIntType",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReplyToMessageType_Actions_ActionTypeId",
                table: "ReplyToMessageType",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SendMessageType_Actions_ActionTypeId",
                table: "SendMessageType",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UptimeType_Actions_ActionTypeId",
                table: "UptimeType",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WatchTimeType_Actions_ActionTypeId",
                table: "WatchTimeType",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WriteFileType_Actions_ActionTypeId",
                table: "WriteFileType",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
