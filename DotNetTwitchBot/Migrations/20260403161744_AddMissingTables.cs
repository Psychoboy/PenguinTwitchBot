using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubActions_Alert_Actions_ActionTypeId",
                table: "SubActions_Alert");

            migrationBuilder.DropForeignKey(
                name: "FK_SubActions_CurrentTime_Actions_ActionTypeId",
                table: "SubActions_CurrentTime");

            migrationBuilder.DropForeignKey(
                name: "FK_SubActions_ExternalApi_Actions_ActionTypeId",
                table: "SubActions_ExternalApi");

            migrationBuilder.DropForeignKey(
                name: "FK_SubActions_FollowAge_Actions_ActionTypeId",
                table: "SubActions_FollowAge");

            migrationBuilder.DropForeignKey(
                name: "FK_SubActions_GiveawayPrize_Actions_ActionTypeId",
                table: "SubActions_GiveawayPrize");

            migrationBuilder.DropForeignKey(
                name: "FK_SubActions_PlaySound_Actions_ActionTypeId",
                table: "SubActions_PlaySound");

            migrationBuilder.DropForeignKey(
                name: "FK_SubActions_RandomInt_Actions_ActionTypeId",
                table: "SubActions_RandomInt");

            migrationBuilder.DropForeignKey(
                name: "FK_SubActions_SendMessage_Actions_ActionTypeId",
                table: "SubActions_SendMessage");

            migrationBuilder.DropForeignKey(
                name: "FK_SubActions_Uptime_Actions_ActionTypeId",
                table: "SubActions_Uptime");

            migrationBuilder.DropForeignKey(
                name: "FK_SubActions_WatchTime_Actions_ActionTypeId",
                table: "SubActions_WatchTime");

            migrationBuilder.DropForeignKey(
                name: "FK_SubActions_WriteFile_Actions_ActionTypeId",
                table: "SubActions_WriteFile");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubActions_WriteFile",
                table: "SubActions_WriteFile");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubActions_WatchTime",
                table: "SubActions_WatchTime");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubActions_Uptime",
                table: "SubActions_Uptime");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubActions_SendMessage",
                table: "SubActions_SendMessage");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubActions_RandomInt",
                table: "SubActions_RandomInt");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubActions_PlaySound",
                table: "SubActions_PlaySound");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubActions_GiveawayPrize",
                table: "SubActions_GiveawayPrize");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubActions_FollowAge",
                table: "SubActions_FollowAge");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubActions_ExternalApi",
                table: "SubActions_ExternalApi");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubActions_CurrentTime",
                table: "SubActions_CurrentTime");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubActions_Alert",
                table: "SubActions_Alert");

            migrationBuilder.RenameTable(
                name: "SubActions_WriteFile",
                newName: "WriteFileType");

            migrationBuilder.RenameTable(
                name: "SubActions_WatchTime",
                newName: "WatchTimeType");

            migrationBuilder.RenameTable(
                name: "SubActions_Uptime",
                newName: "UptimeType");

            migrationBuilder.RenameTable(
                name: "SubActions_SendMessage",
                newName: "SendMessageType");

            migrationBuilder.RenameTable(
                name: "SubActions_RandomInt",
                newName: "RandomIntType");

            migrationBuilder.RenameTable(
                name: "SubActions_PlaySound",
                newName: "PlaySoundType");

            migrationBuilder.RenameTable(
                name: "SubActions_GiveawayPrize",
                newName: "GiveawayPrizeType");

            migrationBuilder.RenameTable(
                name: "SubActions_FollowAge",
                newName: "FollowAgeType");

            migrationBuilder.RenameTable(
                name: "SubActions_ExternalApi",
                newName: "ExternalApiType");

            migrationBuilder.RenameTable(
                name: "SubActions_CurrentTime",
                newName: "CurrentTimeType");

            migrationBuilder.RenameTable(
                name: "SubActions_Alert",
                newName: "AlertType");

            migrationBuilder.RenameIndex(
                name: "IX_SubActions_WriteFile_ActionTypeId",
                table: "WriteFileType",
                newName: "IX_WriteFileType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_SubActions_WatchTime_ActionTypeId",
                table: "WatchTimeType",
                newName: "IX_WatchTimeType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_SubActions_Uptime_ActionTypeId",
                table: "UptimeType",
                newName: "IX_UptimeType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_SubActions_SendMessage_ActionTypeId",
                table: "SendMessageType",
                newName: "IX_SendMessageType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_SubActions_RandomInt_ActionTypeId",
                table: "RandomIntType",
                newName: "IX_RandomIntType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_SubActions_PlaySound_ActionTypeId",
                table: "PlaySoundType",
                newName: "IX_PlaySoundType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_SubActions_GiveawayPrize_ActionTypeId",
                table: "GiveawayPrizeType",
                newName: "IX_GiveawayPrizeType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_SubActions_FollowAge_ActionTypeId",
                table: "FollowAgeType",
                newName: "IX_FollowAgeType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_SubActions_ExternalApi_ActionTypeId",
                table: "ExternalApiType",
                newName: "IX_ExternalApiType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_SubActions_CurrentTime_ActionTypeId",
                table: "CurrentTimeType",
                newName: "IX_CurrentTimeType_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_SubActions_Alert_ActionTypeId",
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
                name: "PK_RandomIntType",
                table: "RandomIntType",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlaySoundType",
                table: "PlaySoundType",
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

            migrationBuilder.CreateTable(
                name: "ReplyToMessageType",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Index = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    File = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SubActionTypes = table.Column<int>(type: "int", nullable: false),
                    ActionTypeId = table.Column<int>(type: "int", nullable: true),
                    UseBot = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FallBack = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    StreamOnly = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplyToMessageType", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReplyToMessageType_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ReplyToMessageType_ActionTypeId",
                table: "ReplyToMessageType",
                column: "ActionTypeId");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
                name: "FK_PlaySoundType_Actions_ActionTypeId",
                table: "PlaySoundType");

            migrationBuilder.DropForeignKey(
                name: "FK_RandomIntType_Actions_ActionTypeId",
                table: "RandomIntType");

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

            migrationBuilder.DropTable(
                name: "ReplyToMessageType");

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
                name: "PK_RandomIntType",
                table: "RandomIntType");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlaySoundType",
                table: "PlaySoundType");

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
                newName: "SubActions_WriteFile");

            migrationBuilder.RenameTable(
                name: "WatchTimeType",
                newName: "SubActions_WatchTime");

            migrationBuilder.RenameTable(
                name: "UptimeType",
                newName: "SubActions_Uptime");

            migrationBuilder.RenameTable(
                name: "SendMessageType",
                newName: "SubActions_SendMessage");

            migrationBuilder.RenameTable(
                name: "RandomIntType",
                newName: "SubActions_RandomInt");

            migrationBuilder.RenameTable(
                name: "PlaySoundType",
                newName: "SubActions_PlaySound");

            migrationBuilder.RenameTable(
                name: "GiveawayPrizeType",
                newName: "SubActions_GiveawayPrize");

            migrationBuilder.RenameTable(
                name: "FollowAgeType",
                newName: "SubActions_FollowAge");

            migrationBuilder.RenameTable(
                name: "ExternalApiType",
                newName: "SubActions_ExternalApi");

            migrationBuilder.RenameTable(
                name: "CurrentTimeType",
                newName: "SubActions_CurrentTime");

            migrationBuilder.RenameTable(
                name: "AlertType",
                newName: "SubActions_Alert");

            migrationBuilder.RenameIndex(
                name: "IX_WriteFileType_ActionTypeId",
                table: "SubActions_WriteFile",
                newName: "IX_SubActions_WriteFile_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_WatchTimeType_ActionTypeId",
                table: "SubActions_WatchTime",
                newName: "IX_SubActions_WatchTime_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_UptimeType_ActionTypeId",
                table: "SubActions_Uptime",
                newName: "IX_SubActions_Uptime_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_SendMessageType_ActionTypeId",
                table: "SubActions_SendMessage",
                newName: "IX_SubActions_SendMessage_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_RandomIntType_ActionTypeId",
                table: "SubActions_RandomInt",
                newName: "IX_SubActions_RandomInt_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_PlaySoundType_ActionTypeId",
                table: "SubActions_PlaySound",
                newName: "IX_SubActions_PlaySound_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_GiveawayPrizeType_ActionTypeId",
                table: "SubActions_GiveawayPrize",
                newName: "IX_SubActions_GiveawayPrize_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_FollowAgeType_ActionTypeId",
                table: "SubActions_FollowAge",
                newName: "IX_SubActions_FollowAge_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_ExternalApiType_ActionTypeId",
                table: "SubActions_ExternalApi",
                newName: "IX_SubActions_ExternalApi_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_CurrentTimeType_ActionTypeId",
                table: "SubActions_CurrentTime",
                newName: "IX_SubActions_CurrentTime_ActionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_AlertType_ActionTypeId",
                table: "SubActions_Alert",
                newName: "IX_SubActions_Alert_ActionTypeId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubActions_WriteFile",
                table: "SubActions_WriteFile",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubActions_WatchTime",
                table: "SubActions_WatchTime",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubActions_Uptime",
                table: "SubActions_Uptime",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubActions_SendMessage",
                table: "SubActions_SendMessage",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubActions_RandomInt",
                table: "SubActions_RandomInt",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubActions_PlaySound",
                table: "SubActions_PlaySound",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubActions_GiveawayPrize",
                table: "SubActions_GiveawayPrize",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubActions_FollowAge",
                table: "SubActions_FollowAge",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubActions_ExternalApi",
                table: "SubActions_ExternalApi",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubActions_CurrentTime",
                table: "SubActions_CurrentTime",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubActions_Alert",
                table: "SubActions_Alert",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SubActions_Alert_Actions_ActionTypeId",
                table: "SubActions_Alert",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SubActions_CurrentTime_Actions_ActionTypeId",
                table: "SubActions_CurrentTime",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SubActions_ExternalApi_Actions_ActionTypeId",
                table: "SubActions_ExternalApi",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SubActions_FollowAge_Actions_ActionTypeId",
                table: "SubActions_FollowAge",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SubActions_GiveawayPrize_Actions_ActionTypeId",
                table: "SubActions_GiveawayPrize",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SubActions_PlaySound_Actions_ActionTypeId",
                table: "SubActions_PlaySound",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SubActions_RandomInt_Actions_ActionTypeId",
                table: "SubActions_RandomInt",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SubActions_SendMessage_Actions_ActionTypeId",
                table: "SubActions_SendMessage",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SubActions_Uptime_Actions_ActionTypeId",
                table: "SubActions_Uptime",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SubActions_WatchTime_Actions_ActionTypeId",
                table: "SubActions_WatchTime",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SubActions_WriteFile_Actions_ActionTypeId",
                table: "SubActions_WriteFile",
                column: "ActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
