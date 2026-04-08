using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class convertIfElseToJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_alert_subactions_logic_if_else_LogicIfElseType_Fa~",
            //    table: "subactions_alert");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_alert");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_break_subactions_logic_if_else_LogicIfElseType_Fa~",
            //    table: "subactions_break");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_break");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_e~",
            //    table: "subactions_channelpointsetenabledstate");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_channelpointsetenabledstate");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_channelpointsetpausedstate");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetpausedstate_subactions_logic_if_el~",
            //    table: "subactions_channelpointsetpausedstate");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_currenttime");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_currenttime_subactions_logic_if_else_LogicIfElseT~",
            //    table: "subactions_currenttime");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_delay");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_delay_subactions_logic_if_else_LogicIfElseType_Fa~",
            //    table: "subactions_delay");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_executeaction");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_executeaction_subactions_logic_if_else_LogicIfEls~",
            //    table: "subactions_executeaction");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_executedefaultcommand");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_executedefaultcommand_subactions_logic_if_else_Lo~",
            //    table: "subactions_executedefaultcommand");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_externalapi");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_externalapi_subactions_logic_if_else_LogicIfElseT~",
            //    table: "subactions_externalapi");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_followage");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_followage_subactions_logic_if_else_LogicIfElseTyp~",
            //    table: "subactions_followage");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_giveawayprize");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_giveawayprize_subactions_logic_if_else_LogicIfEls~",
            //    table: "subactions_giveawayprize");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_logic_if_else");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_logic_if_else_subactions_logic_if_else_LogicIfEls~",
            //    table: "subactions_logic_if_else");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_multicounter");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_multicounter_subactions_logic_if_else_LogicIfElse~",
            //    table: "subactions_multicounter");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_playsound");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_playsound_subactions_logic_if_else_LogicIfElseTyp~",
            //    table: "subactions_playsound");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_randomint");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_randomint_subactions_logic_if_else_LogicIfElseTyp~",
            //    table: "subactions_randomint");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_replytomessage");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_replytomessage_subactions_logic_if_else_LogicIfEl~",
            //    table: "subactions_replytomessage");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_sendmessage");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_sendmessage_subactions_logic_if_else_LogicIfElseT~",
            //    table: "subactions_sendmessage");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_togglecommanddisabled");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_togglecommanddisabled_subactions_logic_if_else_Lo~",
            //    table: "subactions_togglecommanddisabled");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_tts");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_tts_subactions_logic_if_else_LogicIfElseType_Fals~",
            //    table: "subactions_tts");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_uptime");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_uptime_subactions_logic_if_else_LogicIfElseType_F~",
            //    table: "subactions_uptime");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_watchtime");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_watchtime_subactions_logic_if_else_LogicIfElseTyp~",
            //    table: "subactions_watchtime");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
            //    table: "subactions_writefile");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_subactions_writefile_subactions_logic_if_else_LogicIfElseTyp~",
            //    table: "subactions_writefile");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_writefile_LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_writefile");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_writefile_LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_writefile");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_watchtime_LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_watchtime");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_watchtime_LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_watchtime");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_uptime_LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_uptime");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_uptime_LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_uptime");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_tts_LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_tts");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_tts_LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_tts");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_togglecommanddisabled_LogicIfElseType_FalseSubAct~",
            //    table: "subactions_togglecommanddisabled");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_togglecommanddisabled_LogicIfElseType_TrueSubActi~",
            //    table: "subactions_togglecommanddisabled");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_sendmessage_LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_sendmessage");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_sendmessage_LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_sendmessage");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_replytomessage_LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_replytomessage");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_replytomessage_LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_replytomessage");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_randomint_LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_randomint");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_randomint_LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_randomint");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_playsound_LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_playsound");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_playsound_LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_playsound");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_multicounter_LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_multicounter");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_multicounter_LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_multicounter");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_logic_if_else_LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_logic_if_else");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_logic_if_else_LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_logic_if_else");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_giveawayprize_LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_giveawayprize");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_giveawayprize_LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_giveawayprize");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_followage_LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_followage");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_followage_LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_followage");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_externalapi_LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_externalapi");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_externalapi_LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_externalapi");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_executedefaultcommand_LogicIfElseType_FalseSubAct~",
            //    table: "subactions_executedefaultcommand");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_executedefaultcommand_LogicIfElseType_TrueSubActi~",
            //    table: "subactions_executedefaultcommand");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_executeaction_LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_executeaction");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_executeaction_LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_executeaction");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_delay_LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_delay");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_delay_LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_delay");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_currenttime_LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_currenttime");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_currenttime_LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_currenttime");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_channelpointsetpausedstate_LogicIfElseType_FalseS~",
            //    table: "subactions_channelpointsetpausedstate");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_channelpointsetpausedstate_LogicIfElseType_TrueSu~",
            //    table: "subactions_channelpointsetpausedstate");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_channelpointsetenabledstate_LogicIfElseType_False~",
            //    table: "subactions_channelpointsetenabledstate");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_channelpointsetenabledstate_LogicIfElseType_TrueS~",
            //    table: "subactions_channelpointsetenabledstate");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_break_LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_break");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_break_LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_break");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_alert_LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_alert");

            //migrationBuilder.DropIndex(
            //    name: "IX_subactions_alert_LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_alert");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_writefile");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_writefile");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_watchtime");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_watchtime");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_uptime");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_uptime");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_tts");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_tts");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_togglecommanddisabled");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_togglecommanddisabled");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_sendmessage");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_sendmessage");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_replytomessage");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_replytomessage");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_randomint");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_randomint");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_playsound");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_playsound");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_multicounter");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_multicounter");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_logic_if_else");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_logic_if_else");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_giveawayprize");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_giveawayprize");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_followage");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_followage");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_externalapi");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_externalapi");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_executedefaultcommand");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_executedefaultcommand");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_executeaction");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_executeaction");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_delay");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_delay");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_currenttime");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_currenttime");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_channelpointsetpausedstate");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_channelpointsetpausedstate");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_channelpointsetenabledstate");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_channelpointsetenabledstate");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_break");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_break");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_FalseSubActions_Id",
            //    table: "subactions_alert");

            //migrationBuilder.DropColumn(
            //    name: "LogicIfElseType_TrueSubActions_Id",
            //    table: "subactions_alert");

            migrationBuilder.AddColumn<string>(
                name: "FalseSubActions",
                table: "subactions_logic_if_else",
                type: "json",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TrueSubActions",
                table: "subactions_logic_if_else",
                type: "json",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FalseSubActions",
                table: "subactions_logic_if_else");

            migrationBuilder.DropColumn(
                name: "TrueSubActions",
                table: "subactions_logic_if_else");

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_writefile",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_writefile",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_watchtime",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_watchtime",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_uptime",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_uptime",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_tts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_tts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_togglecommanddisabled",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_togglecommanddisabled",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_sendmessage",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_sendmessage",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_replytomessage",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_replytomessage",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_randomint",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_randomint",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_playsound",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_playsound",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_multicounter",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_multicounter",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_logic_if_else",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_logic_if_else",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_giveawayprize",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_giveawayprize",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_followage",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_followage",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_externalapi",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_externalapi",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_executedefaultcommand",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_executedefaultcommand",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_executeaction",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_executeaction",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_delay",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_delay",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_currenttime",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_currenttime",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_channelpointsetpausedstate",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_channelpointsetpausedstate",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_channelpointsetenabledstate",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_channelpointsetenabledstate",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_break",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_break",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_FalseSubActions_Id",
                table: "subactions_alert",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogicIfElseType_TrueSubActions_Id",
                table: "subactions_alert",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_subactions_writefile_LogicIfElseType_FalseSubActions_Id",
                table: "subactions_writefile",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_writefile_LogicIfElseType_TrueSubActions_Id",
                table: "subactions_writefile",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_watchtime_LogicIfElseType_FalseSubActions_Id",
                table: "subactions_watchtime",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_watchtime_LogicIfElseType_TrueSubActions_Id",
                table: "subactions_watchtime",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_uptime_LogicIfElseType_FalseSubActions_Id",
                table: "subactions_uptime",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_uptime_LogicIfElseType_TrueSubActions_Id",
                table: "subactions_uptime",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_tts_LogicIfElseType_FalseSubActions_Id",
                table: "subactions_tts",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_tts_LogicIfElseType_TrueSubActions_Id",
                table: "subactions_tts",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_togglecommanddisabled_LogicIfElseType_FalseSubAct~",
                table: "subactions_togglecommanddisabled",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_togglecommanddisabled_LogicIfElseType_TrueSubActi~",
                table: "subactions_togglecommanddisabled",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_sendmessage_LogicIfElseType_FalseSubActions_Id",
                table: "subactions_sendmessage",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_sendmessage_LogicIfElseType_TrueSubActions_Id",
                table: "subactions_sendmessage",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_replytomessage_LogicIfElseType_FalseSubActions_Id",
                table: "subactions_replytomessage",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_replytomessage_LogicIfElseType_TrueSubActions_Id",
                table: "subactions_replytomessage",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_randomint_LogicIfElseType_FalseSubActions_Id",
                table: "subactions_randomint",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_randomint_LogicIfElseType_TrueSubActions_Id",
                table: "subactions_randomint",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_playsound_LogicIfElseType_FalseSubActions_Id",
                table: "subactions_playsound",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_playsound_LogicIfElseType_TrueSubActions_Id",
                table: "subactions_playsound",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_multicounter_LogicIfElseType_FalseSubActions_Id",
                table: "subactions_multicounter",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_multicounter_LogicIfElseType_TrueSubActions_Id",
                table: "subactions_multicounter",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_logic_if_else_LogicIfElseType_FalseSubActions_Id",
                table: "subactions_logic_if_else",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_logic_if_else_LogicIfElseType_TrueSubActions_Id",
                table: "subactions_logic_if_else",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_giveawayprize_LogicIfElseType_FalseSubActions_Id",
                table: "subactions_giveawayprize",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_giveawayprize_LogicIfElseType_TrueSubActions_Id",
                table: "subactions_giveawayprize",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_followage_LogicIfElseType_FalseSubActions_Id",
                table: "subactions_followage",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_followage_LogicIfElseType_TrueSubActions_Id",
                table: "subactions_followage",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_externalapi_LogicIfElseType_FalseSubActions_Id",
                table: "subactions_externalapi",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_externalapi_LogicIfElseType_TrueSubActions_Id",
                table: "subactions_externalapi",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_executedefaultcommand_LogicIfElseType_FalseSubAct~",
                table: "subactions_executedefaultcommand",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_executedefaultcommand_LogicIfElseType_TrueSubActi~",
                table: "subactions_executedefaultcommand",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_executeaction_LogicIfElseType_FalseSubActions_Id",
                table: "subactions_executeaction",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_executeaction_LogicIfElseType_TrueSubActions_Id",
                table: "subactions_executeaction",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_delay_LogicIfElseType_FalseSubActions_Id",
                table: "subactions_delay",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_delay_LogicIfElseType_TrueSubActions_Id",
                table: "subactions_delay",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_currenttime_LogicIfElseType_FalseSubActions_Id",
                table: "subactions_currenttime",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_currenttime_LogicIfElseType_TrueSubActions_Id",
                table: "subactions_currenttime",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_channelpointsetpausedstate_LogicIfElseType_FalseS~",
                table: "subactions_channelpointsetpausedstate",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_channelpointsetpausedstate_LogicIfElseType_TrueSu~",
                table: "subactions_channelpointsetpausedstate",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_channelpointsetenabledstate_LogicIfElseType_False~",
                table: "subactions_channelpointsetenabledstate",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_channelpointsetenabledstate_LogicIfElseType_TrueS~",
                table: "subactions_channelpointsetenabledstate",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_break_LogicIfElseType_FalseSubActions_Id",
                table: "subactions_break",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_break_LogicIfElseType_TrueSubActions_Id",
                table: "subactions_break",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_alert_LogicIfElseType_FalseSubActions_Id",
                table: "subactions_alert",
                column: "LogicIfElseType_FalseSubActions_Id");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_alert_LogicIfElseType_TrueSubActions_Id",
                table: "subactions_alert",
                column: "LogicIfElseType_TrueSubActions_Id");

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_alert_subactions_logic_if_else_LogicIfElseType_Fa~",
                table: "subactions_alert",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_alert",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_break_subactions_logic_if_else_LogicIfElseType_Fa~",
                table: "subactions_break",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_break",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_e~",
                table: "subactions_channelpointsetenabledstate",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_channelpointsetenabledstate",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_channelpointsetpausedstate",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetpausedstate_subactions_logic_if_el~",
                table: "subactions_channelpointsetpausedstate",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_currenttime",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_currenttime_subactions_logic_if_else_LogicIfElseT~",
                table: "subactions_currenttime",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_delay",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_delay_subactions_logic_if_else_LogicIfElseType_Fa~",
                table: "subactions_delay",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_executeaction",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_executeaction_subactions_logic_if_else_LogicIfEls~",
                table: "subactions_executeaction",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_executedefaultcommand",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_executedefaultcommand_subactions_logic_if_else_Lo~",
                table: "subactions_executedefaultcommand",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_externalapi",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_externalapi_subactions_logic_if_else_LogicIfElseT~",
                table: "subactions_externalapi",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_followage",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_followage_subactions_logic_if_else_LogicIfElseTyp~",
                table: "subactions_followage",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_giveawayprize",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_giveawayprize_subactions_logic_if_else_LogicIfEls~",
                table: "subactions_giveawayprize",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_logic_if_else",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_logic_if_else_subactions_logic_if_else_LogicIfEls~",
                table: "subactions_logic_if_else",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_multicounter",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_multicounter_subactions_logic_if_else_LogicIfElse~",
                table: "subactions_multicounter",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_playsound",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_playsound_subactions_logic_if_else_LogicIfElseTyp~",
                table: "subactions_playsound",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_randomint",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_randomint_subactions_logic_if_else_LogicIfElseTyp~",
                table: "subactions_randomint",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_replytomessage",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_replytomessage_subactions_logic_if_else_LogicIfEl~",
                table: "subactions_replytomessage",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_sendmessage",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_sendmessage_subactions_logic_if_else_LogicIfElseT~",
                table: "subactions_sendmessage",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_togglecommanddisabled",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_togglecommanddisabled_subactions_logic_if_else_Lo~",
                table: "subactions_togglecommanddisabled",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_tts",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_tts_subactions_logic_if_else_LogicIfElseType_Fals~",
                table: "subactions_tts",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_uptime",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_uptime_subactions_logic_if_else_LogicIfElseType_F~",
                table: "subactions_uptime",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_watchtime",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_watchtime_subactions_logic_if_else_LogicIfElseTyp~",
                table: "subactions_watchtime",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_channelpointsetenabledstate_subactions_logic_if_~1",
                table: "subactions_writefile",
                column: "LogicIfElseType_TrueSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subactions_writefile_subactions_logic_if_else_LogicIfElseTyp~",
                table: "subactions_writefile",
                column: "LogicIfElseType_FalseSubActions_Id",
                principalTable: "subactions_logic_if_else",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
