using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations;

/// <inheritdoc />
public partial class _20261126000439_AddMissingQueryIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_IpLogEntrys_UserId",
            table: "IpLogEntrys");

        migrationBuilder.CreateIndex(
            name: "IX_ViewersTime_banned_Time",
            table: "ViewersTime",
            columns: new[] { "banned", "Time" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_Viewers_isEditor",
            table: "Viewers",
            column: "isEditor",
            filter: "\"isEditor\" = true");

        migrationBuilder.CreateIndex(
            name: "IX_Viewers_isSub",
            table: "Viewers",
            column: "isSub",
            filter: "\"isSub\" = true");

        migrationBuilder.CreateIndex(
            name: "IX_ViewerMessageCounts_banned_MessageCount",
            table: "ViewerMessageCounts",
            columns: new[] { "banned", "MessageCount" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_UserRegisteredVoices_Username",
            table: "UserRegisteredVoices",
            column: "Username");

        migrationBuilder.CreateIndex(
            name: "IX_Triggers_Type_DefaultCommandId",
            table: "Triggers",
            columns: new[] { "Type", "DefaultCommandId" },
            filter: "\"DefaultCommandId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Triggers_Type_Enabled_CommandId",
            table: "Triggers",
            columns: new[] { "Type", "Enabled", "CommandId" },
            filter: "\"CommandId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Triggers_Type_Enabled_KeywordId",
            table: "Triggers",
            columns: new[] { "Type", "Enabled", "KeywordId" },
            filter: "\"KeywordId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Triggers_Type_Enabled_Name",
            table: "Triggers",
            columns: new[] { "Type", "Enabled", "Name" });

        migrationBuilder.CreateIndex(
            name: "IX_Triggers_Type_TimerGroupId",
            table: "Triggers",
            columns: new[] { "Type", "TimerGroupId" },
            filter: "\"TimerGroupId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_TimerGroups_Active_NextRun",
            table: "TimerGroups",
            columns: new[] { "Active", "NextRun" });

        migrationBuilder.CreateIndex(
            name: "IX_Songs_SongId",
            table: "Songs",
            column: "SongId");

        migrationBuilder.CreateIndex(
            name: "IX_SongRequestHistories_RequestDate_SongId",
            table: "SongRequestHistories",
            columns: new[] { "RequestDate", "SongId" });

        migrationBuilder.CreateIndex(
            name: "IX_SongCooldowns_CooldownExpiresAt",
            table: "SongCooldowns",
            column: "CooldownExpiresAt");

        migrationBuilder.CreateIndex(
            name: "IX_RaidHistory_LastCheckOnline",
            table: "RaidHistory",
            column: "LastCheckOnline");

        migrationBuilder.CreateIndex(
            name: "IX_RaidHistory_UserId",
            table: "RaidHistory",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_IpLogEntrys_ConnectedDate",
            table: "IpLogEntrys",
            column: "ConnectedDate");

        migrationBuilder.CreateIndex(
            name: "IX_IpLogEntrys_Ip",
            table: "IpLogEntrys",
            column: "Ip");

        migrationBuilder.CreateIndex(
            name: "IX_IpLogEntrys_Username",
            table: "IpLogEntrys",
            column: "Username");

        migrationBuilder.CreateIndex(
            name: "IX_Followers_FollowDate_Username",
            table: "Followers",
            columns: new[] { "FollowDate", "Username" },
            descending: new[] { true, false });

        migrationBuilder.CreateIndex(
            name: "IX_DiscordEvents_DiscordEventId",
            table: "DiscordEvents",
            column: "DiscordEventId");

        migrationBuilder.CreateIndex(
            name: "IX_DiscordEvents_TwitchEventId",
            table: "DiscordEvents",
            column: "TwitchEventId");

        migrationBuilder.CreateIndex(
            name: "IX_DefaultCommands_ModuleName",
            table: "DefaultCommands",
            column: "ModuleName");

        migrationBuilder.CreateIndex(
            name: "IX_Aliases_AliasName",
            table: "Aliases",
            column: "AliasName");

        migrationBuilder.CreateIndex(
            name: "IX_Actions_Name",
            table: "Actions",
            column: "Name");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ViewersTime_banned_Time",
            table: "ViewersTime");

        migrationBuilder.DropIndex(
            name: "IX_Viewers_isEditor",
            table: "Viewers");

        migrationBuilder.DropIndex(
            name: "IX_Viewers_isSub",
            table: "Viewers");

        migrationBuilder.DropIndex(
            name: "IX_ViewerMessageCounts_banned_MessageCount",
            table: "ViewerMessageCounts");

        migrationBuilder.DropIndex(
            name: "IX_UserRegisteredVoices_Username",
            table: "UserRegisteredVoices");

        migrationBuilder.DropIndex(
            name: "IX_Triggers_Type_DefaultCommandId",
            table: "Triggers");

        migrationBuilder.DropIndex(
            name: "IX_Triggers_Type_Enabled_CommandId",
            table: "Triggers");

        migrationBuilder.DropIndex(
            name: "IX_Triggers_Type_Enabled_KeywordId",
            table: "Triggers");

        migrationBuilder.DropIndex(
            name: "IX_Triggers_Type_Enabled_Name",
            table: "Triggers");

        migrationBuilder.DropIndex(
            name: "IX_Triggers_Type_TimerGroupId",
            table: "Triggers");

        migrationBuilder.DropIndex(
            name: "IX_TimerGroups_Active_NextRun",
            table: "TimerGroups");

        migrationBuilder.DropIndex(
            name: "IX_Songs_SongId",
            table: "Songs");

        migrationBuilder.DropIndex(
            name: "IX_SongRequestHistories_RequestDate_SongId",
            table: "SongRequestHistories");

        migrationBuilder.DropIndex(
            name: "IX_SongCooldowns_CooldownExpiresAt",
            table: "SongCooldowns");

        migrationBuilder.DropIndex(
            name: "IX_RaidHistory_LastCheckOnline",
            table: "RaidHistory");

        migrationBuilder.DropIndex(
            name: "IX_RaidHistory_UserId",
            table: "RaidHistory");

        migrationBuilder.DropIndex(
            name: "IX_IpLogEntrys_ConnectedDate",
            table: "IpLogEntrys");

        migrationBuilder.DropIndex(
            name: "IX_IpLogEntrys_Ip",
            table: "IpLogEntrys");

        migrationBuilder.DropIndex(
            name: "IX_IpLogEntrys_Username",
            table: "IpLogEntrys");

        migrationBuilder.DropIndex(
            name: "IX_Followers_FollowDate_Username",
            table: "Followers");

        migrationBuilder.DropIndex(
            name: "IX_DiscordEvents_DiscordEventId",
            table: "DiscordEvents");

        migrationBuilder.DropIndex(
            name: "IX_DiscordEvents_TwitchEventId",
            table: "DiscordEvents");

        migrationBuilder.DropIndex(
            name: "IX_DefaultCommands_ModuleName",
            table: "DefaultCommands");

        migrationBuilder.DropIndex(
            name: "IX_Aliases_AliasName",
            table: "Aliases");

        migrationBuilder.DropIndex(
            name: "IX_Actions_Name",
            table: "Actions");

        migrationBuilder.CreateIndex(
            name: "IX_IpLogEntrys_UserId",
            table: "IpLogEntrys",
            column: "UserId");
    }
}
