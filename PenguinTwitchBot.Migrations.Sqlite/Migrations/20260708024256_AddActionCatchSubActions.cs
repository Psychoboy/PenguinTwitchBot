using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddActionCatchSubActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_writefile",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_watchtime",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_uptime",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_tts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_togglecommanddisabled",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_timergroupsetenabled",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_setvariable",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_sendmessage",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_replytomessage",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_randomint",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_rafflestart",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_rafflesetwinnercount",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_rafflesettotalaward",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_rafflegetentrycount",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_raffleenter",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_raffleend",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_pointcommand",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_playsound",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_obs_triggerhotkey",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_obs_settext",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_obs_setsourcevisibility",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_obs_setsourcefilterstate",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_obs_setsourceaudiotrackstate",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_obs_setscenefilterstate",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_obs_setscene",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_obs_setmediastate",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_obs_setmediasourcefile",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_obs_setinputmute",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_obs_setimagesourcefile",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_obs_setcolorsourcecolor",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_obs_setbrowsersourceurl",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_multicounter",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_logic_if_else",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_giveawayprize",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_giftpoints",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_foreachviewer",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_followage",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_fishingtournamentstart",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_fishingtournamentend",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_fishingtournamenteligiblecatch",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_fishing",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_externalapi",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_executedefaultcommand",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_executeaction",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_delay",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_currenttime",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_checkpoints",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_channelpointsetpausedstate",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_channelpointsetenabledstate",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_break",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatchActionTypeId",
                table: "subactions_alert",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_subactions_writefile_CatchActionTypeId",
                table: "subactions_writefile",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_watchtime_CatchActionTypeId",
                table: "subactions_watchtime",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_uptime_CatchActionTypeId",
                table: "subactions_uptime",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_tts_CatchActionTypeId",
                table: "subactions_tts",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_togglecommanddisabled_CatchActionTypeId",
                table: "subactions_togglecommanddisabled",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_timergroupsetenabled_CatchActionTypeId",
                table: "subactions_timergroupsetenabled",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_setvariable_CatchActionTypeId",
                table: "subactions_setvariable",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_sendmessage_CatchActionTypeId",
                table: "subactions_sendmessage",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_replytomessage_CatchActionTypeId",
                table: "subactions_replytomessage",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_randomint_CatchActionTypeId",
                table: "subactions_randomint",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_rafflestart_CatchActionTypeId",
                table: "subactions_rafflestart",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_rafflesetwinnercount_CatchActionTypeId",
                table: "subactions_rafflesetwinnercount",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_rafflesettotalaward_CatchActionTypeId",
                table: "subactions_rafflesettotalaward",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_rafflegetentrycount_CatchActionTypeId",
                table: "subactions_rafflegetentrycount",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_raffleenter_CatchActionTypeId",
                table: "subactions_raffleenter",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_raffleend_CatchActionTypeId",
                table: "subactions_raffleend",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_pointcommand_CatchActionTypeId",
                table: "subactions_pointcommand",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_playsound_CatchActionTypeId",
                table: "subactions_playsound",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_triggerhotkey_CatchActionTypeId",
                table: "subactions_obs_triggerhotkey",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_settext_CatchActionTypeId",
                table: "subactions_obs_settext",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setsourcevisibility_CatchActionTypeId",
                table: "subactions_obs_setsourcevisibility",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setsourcefilterstate_CatchActionTypeId",
                table: "subactions_obs_setsourcefilterstate",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setsourceaudiotrackstate_CatchActionTypeId",
                table: "subactions_obs_setsourceaudiotrackstate",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setscenefilterstate_CatchActionTypeId",
                table: "subactions_obs_setscenefilterstate",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setscene_CatchActionTypeId",
                table: "subactions_obs_setscene",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setmediastate_CatchActionTypeId",
                table: "subactions_obs_setmediastate",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setmediasourcefile_CatchActionTypeId",
                table: "subactions_obs_setmediasourcefile",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setinputmute_CatchActionTypeId",
                table: "subactions_obs_setinputmute",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setimagesourcefile_CatchActionTypeId",
                table: "subactions_obs_setimagesourcefile",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setcolorsourcecolor_CatchActionTypeId",
                table: "subactions_obs_setcolorsourcecolor",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setbrowsersourceurl_CatchActionTypeId",
                table: "subactions_obs_setbrowsersourceurl",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_multicounter_CatchActionTypeId",
                table: "subactions_multicounter",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_logic_if_else_CatchActionTypeId",
                table: "subactions_logic_if_else",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_giveawayprize_CatchActionTypeId",
                table: "subactions_giveawayprize",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_giftpoints_CatchActionTypeId",
                table: "subactions_giftpoints",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_foreachviewer_CatchActionTypeId",
                table: "subactions_foreachviewer",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_followage_CatchActionTypeId",
                table: "subactions_followage",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_fishingtournamentstart_CatchActionTypeId",
                table: "subactions_fishingtournamentstart",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_fishingtournamentend_CatchActionTypeId",
                table: "subactions_fishingtournamentend",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_fishingtournamenteligiblecatch_CatchActionTypeId",
                table: "subactions_fishingtournamenteligiblecatch",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_fishing_CatchActionTypeId",
                table: "subactions_fishing",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_externalapi_CatchActionTypeId",
                table: "subactions_externalapi",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_executedefaultcommand_CatchActionTypeId",
                table: "subactions_executedefaultcommand",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_executeaction_CatchActionTypeId",
                table: "subactions_executeaction",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_delay_CatchActionTypeId",
                table: "subactions_delay",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_currenttime_CatchActionTypeId",
                table: "subactions_currenttime",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_checkpoints_CatchActionTypeId",
                table: "subactions_checkpoints",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_channelpointsetpausedstate_CatchActionTypeId",
                table: "subactions_channelpointsetpausedstate",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_channelpointsetenabledstate_CatchActionTypeId",
                table: "subactions_channelpointsetenabledstate",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_break_CatchActionTypeId",
                table: "subactions_break",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_alert_CatchActionTypeId",
                table: "subactions_alert",
                column: "CatchActionTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_alert_Actions_CatchActionTypeId",
                table: "subactions_alert",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_break_Actions_CatchActionTypeId",
                table: "subactions_break",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_Actions_CatchActionTypeId",
                table: "subactions_channelpointsetenabledstate",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetpausedstate_Actions_CatchActionTypeId",
                table: "subactions_channelpointsetpausedstate",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_checkpoints_Actions_CatchActionTypeId",
                table: "subactions_checkpoints",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_currenttime_Actions_CatchActionTypeId",
                table: "subactions_currenttime",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_delay_Actions_CatchActionTypeId",
                table: "subactions_delay",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_executeaction_Actions_CatchActionTypeId",
                table: "subactions_executeaction",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_executedefaultcommand_Actions_CatchActionTypeId",
                table: "subactions_executedefaultcommand",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_externalapi_Actions_CatchActionTypeId",
                table: "subactions_externalapi",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_fishing_Actions_CatchActionTypeId",
                table: "subactions_fishing",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_fishingtournamenteligiblecatch_Actions_CatchActionTypeId",
                table: "subactions_fishingtournamenteligiblecatch",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_fishingtournamentend_Actions_CatchActionTypeId",
                table: "subactions_fishingtournamentend",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_fishingtournamentstart_Actions_CatchActionTypeId",
                table: "subactions_fishingtournamentstart",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_followage_Actions_CatchActionTypeId",
                table: "subactions_followage",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_foreachviewer_Actions_CatchActionTypeId",
                table: "subactions_foreachviewer",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_giftpoints_Actions_CatchActionTypeId",
                table: "subactions_giftpoints",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_giveawayprize_Actions_CatchActionTypeId",
                table: "subactions_giveawayprize",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_logic_if_else_Actions_CatchActionTypeId",
                table: "subactions_logic_if_else",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_multicounter_Actions_CatchActionTypeId",
                table: "subactions_multicounter",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_obs_setbrowsersourceurl_Actions_CatchActionTypeId",
                table: "subactions_obs_setbrowsersourceurl",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_obs_setcolorsourcecolor_Actions_CatchActionTypeId",
                table: "subactions_obs_setcolorsourcecolor",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_obs_setimagesourcefile_Actions_CatchActionTypeId",
                table: "subactions_obs_setimagesourcefile",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_obs_setinputmute_Actions_CatchActionTypeId",
                table: "subactions_obs_setinputmute",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_obs_setmediasourcefile_Actions_CatchActionTypeId",
                table: "subactions_obs_setmediasourcefile",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_obs_setmediastate_Actions_CatchActionTypeId",
                table: "subactions_obs_setmediastate",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_obs_setscene_Actions_CatchActionTypeId",
                table: "subactions_obs_setscene",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_obs_setscenefilterstate_Actions_CatchActionTypeId",
                table: "subactions_obs_setscenefilterstate",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_obs_setsourceaudiotrackstate_Actions_CatchActionTypeId",
                table: "subactions_obs_setsourceaudiotrackstate",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_obs_setsourcefilterstate_Actions_CatchActionTypeId",
                table: "subactions_obs_setsourcefilterstate",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_obs_setsourcevisibility_Actions_CatchActionTypeId",
                table: "subactions_obs_setsourcevisibility",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_obs_settext_Actions_CatchActionTypeId",
                table: "subactions_obs_settext",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_obs_triggerhotkey_Actions_CatchActionTypeId",
                table: "subactions_obs_triggerhotkey",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_playsound_Actions_CatchActionTypeId",
                table: "subactions_playsound",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_pointcommand_Actions_CatchActionTypeId",
                table: "subactions_pointcommand",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_raffleend_Actions_CatchActionTypeId",
                table: "subactions_raffleend",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_raffleenter_Actions_CatchActionTypeId",
                table: "subactions_raffleenter",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_rafflegetentrycount_Actions_CatchActionTypeId",
                table: "subactions_rafflegetentrycount",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_rafflesettotalaward_Actions_CatchActionTypeId",
                table: "subactions_rafflesettotalaward",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_rafflesetwinnercount_Actions_CatchActionTypeId",
                table: "subactions_rafflesetwinnercount",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_rafflestart_Actions_CatchActionTypeId",
                table: "subactions_rafflestart",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_randomint_Actions_CatchActionTypeId",
                table: "subactions_randomint",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_replytomessage_Actions_CatchActionTypeId",
                table: "subactions_replytomessage",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_sendmessage_Actions_CatchActionTypeId",
                table: "subactions_sendmessage",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_setvariable_Actions_CatchActionTypeId",
                table: "subactions_setvariable",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_timergroupsetenabled_Actions_CatchActionTypeId",
                table: "subactions_timergroupsetenabled",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_togglecommanddisabled_Actions_CatchActionTypeId",
                table: "subactions_togglecommanddisabled",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_tts_Actions_CatchActionTypeId",
                table: "subactions_tts",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_uptime_Actions_CatchActionTypeId",
                table: "subactions_uptime",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_watchtime_Actions_CatchActionTypeId",
                table: "subactions_watchtime",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_writefile_Actions_CatchActionTypeId",
                table: "subactions_writefile",
                column: "CatchActionTypeId",
                principalTable: "Actions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_subactions_alert_Actions_CatchActionTypeId",
                table: "subactions_alert");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_break_Actions_CatchActionTypeId",
                table: "subactions_break");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_Actions_CatchActionTypeId",
                table: "subactions_channelpointsetenabledstate");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_channelpointsetpausedstate_Actions_CatchActionTypeId",
                table: "subactions_channelpointsetpausedstate");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_checkpoints_Actions_CatchActionTypeId",
                table: "subactions_checkpoints");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_currenttime_Actions_CatchActionTypeId",
                table: "subactions_currenttime");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_delay_Actions_CatchActionTypeId",
                table: "subactions_delay");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_executeaction_Actions_CatchActionTypeId",
                table: "subactions_executeaction");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_executedefaultcommand_Actions_CatchActionTypeId",
                table: "subactions_executedefaultcommand");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_externalapi_Actions_CatchActionTypeId",
                table: "subactions_externalapi");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_fishing_Actions_CatchActionTypeId",
                table: "subactions_fishing");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_fishingtournamenteligiblecatch_Actions_CatchActionTypeId",
                table: "subactions_fishingtournamenteligiblecatch");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_fishingtournamentend_Actions_CatchActionTypeId",
                table: "subactions_fishingtournamentend");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_fishingtournamentstart_Actions_CatchActionTypeId",
                table: "subactions_fishingtournamentstart");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_followage_Actions_CatchActionTypeId",
                table: "subactions_followage");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_foreachviewer_Actions_CatchActionTypeId",
                table: "subactions_foreachviewer");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_giftpoints_Actions_CatchActionTypeId",
                table: "subactions_giftpoints");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_giveawayprize_Actions_CatchActionTypeId",
                table: "subactions_giveawayprize");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_logic_if_else_Actions_CatchActionTypeId",
                table: "subactions_logic_if_else");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_multicounter_Actions_CatchActionTypeId",
                table: "subactions_multicounter");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_obs_setbrowsersourceurl_Actions_CatchActionTypeId",
                table: "subactions_obs_setbrowsersourceurl");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_obs_setcolorsourcecolor_Actions_CatchActionTypeId",
                table: "subactions_obs_setcolorsourcecolor");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_obs_setimagesourcefile_Actions_CatchActionTypeId",
                table: "subactions_obs_setimagesourcefile");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_obs_setinputmute_Actions_CatchActionTypeId",
                table: "subactions_obs_setinputmute");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_obs_setmediasourcefile_Actions_CatchActionTypeId",
                table: "subactions_obs_setmediasourcefile");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_obs_setmediastate_Actions_CatchActionTypeId",
                table: "subactions_obs_setmediastate");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_obs_setscene_Actions_CatchActionTypeId",
                table: "subactions_obs_setscene");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_obs_setscenefilterstate_Actions_CatchActionTypeId",
                table: "subactions_obs_setscenefilterstate");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_obs_setsourceaudiotrackstate_Actions_CatchActionTypeId",
                table: "subactions_obs_setsourceaudiotrackstate");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_obs_setsourcefilterstate_Actions_CatchActionTypeId",
                table: "subactions_obs_setsourcefilterstate");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_obs_setsourcevisibility_Actions_CatchActionTypeId",
                table: "subactions_obs_setsourcevisibility");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_obs_settext_Actions_CatchActionTypeId",
                table: "subactions_obs_settext");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_obs_triggerhotkey_Actions_CatchActionTypeId",
                table: "subactions_obs_triggerhotkey");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_playsound_Actions_CatchActionTypeId",
                table: "subactions_playsound");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_pointcommand_Actions_CatchActionTypeId",
                table: "subactions_pointcommand");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_raffleend_Actions_CatchActionTypeId",
                table: "subactions_raffleend");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_raffleenter_Actions_CatchActionTypeId",
                table: "subactions_raffleenter");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_rafflegetentrycount_Actions_CatchActionTypeId",
                table: "subactions_rafflegetentrycount");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_rafflesettotalaward_Actions_CatchActionTypeId",
                table: "subactions_rafflesettotalaward");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_rafflesetwinnercount_Actions_CatchActionTypeId",
                table: "subactions_rafflesetwinnercount");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_rafflestart_Actions_CatchActionTypeId",
                table: "subactions_rafflestart");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_randomint_Actions_CatchActionTypeId",
                table: "subactions_randomint");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_replytomessage_Actions_CatchActionTypeId",
                table: "subactions_replytomessage");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_sendmessage_Actions_CatchActionTypeId",
                table: "subactions_sendmessage");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_setvariable_Actions_CatchActionTypeId",
                table: "subactions_setvariable");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_timergroupsetenabled_Actions_CatchActionTypeId",
                table: "subactions_timergroupsetenabled");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_togglecommanddisabled_Actions_CatchActionTypeId",
                table: "subactions_togglecommanddisabled");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_tts_Actions_CatchActionTypeId",
                table: "subactions_tts");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_uptime_Actions_CatchActionTypeId",
                table: "subactions_uptime");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_watchtime_Actions_CatchActionTypeId",
                table: "subactions_watchtime");

            migrationBuilder.DropForeignKey(
                name: "FK_subactions_writefile_Actions_CatchActionTypeId",
                table: "subactions_writefile");

            migrationBuilder.DropIndex(
                name: "IX_subactions_writefile_CatchActionTypeId",
                table: "subactions_writefile");

            migrationBuilder.DropIndex(
                name: "IX_subactions_watchtime_CatchActionTypeId",
                table: "subactions_watchtime");

            migrationBuilder.DropIndex(
                name: "IX_subactions_uptime_CatchActionTypeId",
                table: "subactions_uptime");

            migrationBuilder.DropIndex(
                name: "IX_subactions_tts_CatchActionTypeId",
                table: "subactions_tts");

            migrationBuilder.DropIndex(
                name: "IX_subactions_togglecommanddisabled_CatchActionTypeId",
                table: "subactions_togglecommanddisabled");

            migrationBuilder.DropIndex(
                name: "IX_subactions_timergroupsetenabled_CatchActionTypeId",
                table: "subactions_timergroupsetenabled");

            migrationBuilder.DropIndex(
                name: "IX_subactions_setvariable_CatchActionTypeId",
                table: "subactions_setvariable");

            migrationBuilder.DropIndex(
                name: "IX_subactions_sendmessage_CatchActionTypeId",
                table: "subactions_sendmessage");

            migrationBuilder.DropIndex(
                name: "IX_subactions_replytomessage_CatchActionTypeId",
                table: "subactions_replytomessage");

            migrationBuilder.DropIndex(
                name: "IX_subactions_randomint_CatchActionTypeId",
                table: "subactions_randomint");

            migrationBuilder.DropIndex(
                name: "IX_subactions_rafflestart_CatchActionTypeId",
                table: "subactions_rafflestart");

            migrationBuilder.DropIndex(
                name: "IX_subactions_rafflesetwinnercount_CatchActionTypeId",
                table: "subactions_rafflesetwinnercount");

            migrationBuilder.DropIndex(
                name: "IX_subactions_rafflesettotalaward_CatchActionTypeId",
                table: "subactions_rafflesettotalaward");

            migrationBuilder.DropIndex(
                name: "IX_subactions_rafflegetentrycount_CatchActionTypeId",
                table: "subactions_rafflegetentrycount");

            migrationBuilder.DropIndex(
                name: "IX_subactions_raffleenter_CatchActionTypeId",
                table: "subactions_raffleenter");

            migrationBuilder.DropIndex(
                name: "IX_subactions_raffleend_CatchActionTypeId",
                table: "subactions_raffleend");

            migrationBuilder.DropIndex(
                name: "IX_subactions_pointcommand_CatchActionTypeId",
                table: "subactions_pointcommand");

            migrationBuilder.DropIndex(
                name: "IX_subactions_playsound_CatchActionTypeId",
                table: "subactions_playsound");

            migrationBuilder.DropIndex(
                name: "IX_subactions_obs_triggerhotkey_CatchActionTypeId",
                table: "subactions_obs_triggerhotkey");

            migrationBuilder.DropIndex(
                name: "IX_subactions_obs_settext_CatchActionTypeId",
                table: "subactions_obs_settext");

            migrationBuilder.DropIndex(
                name: "IX_subactions_obs_setsourcevisibility_CatchActionTypeId",
                table: "subactions_obs_setsourcevisibility");

            migrationBuilder.DropIndex(
                name: "IX_subactions_obs_setsourcefilterstate_CatchActionTypeId",
                table: "subactions_obs_setsourcefilterstate");

            migrationBuilder.DropIndex(
                name: "IX_subactions_obs_setsourceaudiotrackstate_CatchActionTypeId",
                table: "subactions_obs_setsourceaudiotrackstate");

            migrationBuilder.DropIndex(
                name: "IX_subactions_obs_setscenefilterstate_CatchActionTypeId",
                table: "subactions_obs_setscenefilterstate");

            migrationBuilder.DropIndex(
                name: "IX_subactions_obs_setscene_CatchActionTypeId",
                table: "subactions_obs_setscene");

            migrationBuilder.DropIndex(
                name: "IX_subactions_obs_setmediastate_CatchActionTypeId",
                table: "subactions_obs_setmediastate");

            migrationBuilder.DropIndex(
                name: "IX_subactions_obs_setmediasourcefile_CatchActionTypeId",
                table: "subactions_obs_setmediasourcefile");

            migrationBuilder.DropIndex(
                name: "IX_subactions_obs_setinputmute_CatchActionTypeId",
                table: "subactions_obs_setinputmute");

            migrationBuilder.DropIndex(
                name: "IX_subactions_obs_setimagesourcefile_CatchActionTypeId",
                table: "subactions_obs_setimagesourcefile");

            migrationBuilder.DropIndex(
                name: "IX_subactions_obs_setcolorsourcecolor_CatchActionTypeId",
                table: "subactions_obs_setcolorsourcecolor");

            migrationBuilder.DropIndex(
                name: "IX_subactions_obs_setbrowsersourceurl_CatchActionTypeId",
                table: "subactions_obs_setbrowsersourceurl");

            migrationBuilder.DropIndex(
                name: "IX_subactions_multicounter_CatchActionTypeId",
                table: "subactions_multicounter");

            migrationBuilder.DropIndex(
                name: "IX_subactions_logic_if_else_CatchActionTypeId",
                table: "subactions_logic_if_else");

            migrationBuilder.DropIndex(
                name: "IX_subactions_giveawayprize_CatchActionTypeId",
                table: "subactions_giveawayprize");

            migrationBuilder.DropIndex(
                name: "IX_subactions_giftpoints_CatchActionTypeId",
                table: "subactions_giftpoints");

            migrationBuilder.DropIndex(
                name: "IX_subactions_foreachviewer_CatchActionTypeId",
                table: "subactions_foreachviewer");

            migrationBuilder.DropIndex(
                name: "IX_subactions_followage_CatchActionTypeId",
                table: "subactions_followage");

            migrationBuilder.DropIndex(
                name: "IX_subactions_fishingtournamentstart_CatchActionTypeId",
                table: "subactions_fishingtournamentstart");

            migrationBuilder.DropIndex(
                name: "IX_subactions_fishingtournamentend_CatchActionTypeId",
                table: "subactions_fishingtournamentend");

            migrationBuilder.DropIndex(
                name: "IX_subactions_fishingtournamenteligiblecatch_CatchActionTypeId",
                table: "subactions_fishingtournamenteligiblecatch");

            migrationBuilder.DropIndex(
                name: "IX_subactions_fishing_CatchActionTypeId",
                table: "subactions_fishing");

            migrationBuilder.DropIndex(
                name: "IX_subactions_externalapi_CatchActionTypeId",
                table: "subactions_externalapi");

            migrationBuilder.DropIndex(
                name: "IX_subactions_executedefaultcommand_CatchActionTypeId",
                table: "subactions_executedefaultcommand");

            migrationBuilder.DropIndex(
                name: "IX_subactions_executeaction_CatchActionTypeId",
                table: "subactions_executeaction");

            migrationBuilder.DropIndex(
                name: "IX_subactions_delay_CatchActionTypeId",
                table: "subactions_delay");

            migrationBuilder.DropIndex(
                name: "IX_subactions_currenttime_CatchActionTypeId",
                table: "subactions_currenttime");

            migrationBuilder.DropIndex(
                name: "IX_subactions_checkpoints_CatchActionTypeId",
                table: "subactions_checkpoints");

            migrationBuilder.DropIndex(
                name: "IX_subactions_channelpointsetpausedstate_CatchActionTypeId",
                table: "subactions_channelpointsetpausedstate");

            migrationBuilder.DropIndex(
                name: "IX_subactions_channelpointsetenabledstate_CatchActionTypeId",
                table: "subactions_channelpointsetenabledstate");

            migrationBuilder.DropIndex(
                name: "IX_subactions_break_CatchActionTypeId",
                table: "subactions_break");

            migrationBuilder.DropIndex(
                name: "IX_subactions_alert_CatchActionTypeId",
                table: "subactions_alert");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_writefile");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_watchtime");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_uptime");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_tts");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_togglecommanddisabled");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_timergroupsetenabled");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_setvariable");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_sendmessage");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_replytomessage");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_randomint");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_rafflestart");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_rafflesetwinnercount");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_rafflesettotalaward");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_rafflegetentrycount");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_raffleenter");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_raffleend");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_pointcommand");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_playsound");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_obs_triggerhotkey");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_obs_settext");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_obs_setsourcevisibility");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_obs_setsourcefilterstate");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_obs_setsourceaudiotrackstate");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_obs_setscenefilterstate");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_obs_setscene");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_obs_setmediastate");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_obs_setmediasourcefile");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_obs_setinputmute");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_obs_setimagesourcefile");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_obs_setcolorsourcecolor");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_obs_setbrowsersourceurl");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_multicounter");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_logic_if_else");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_giveawayprize");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_giftpoints");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_foreachviewer");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_followage");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_fishingtournamentstart");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_fishingtournamentend");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_fishingtournamenteligiblecatch");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_fishing");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_externalapi");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_executedefaultcommand");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_executeaction");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_delay");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_currenttime");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_checkpoints");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_channelpointsetpausedstate");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_channelpointsetenabledstate");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_break");

            migrationBuilder.DropColumn(
                name: "CatchActionTypeId",
                table: "subactions_alert");
        }
    }
}
