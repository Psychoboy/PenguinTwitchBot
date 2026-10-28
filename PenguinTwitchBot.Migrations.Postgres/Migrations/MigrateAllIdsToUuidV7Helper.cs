using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
{
    public static class MigrateAllIdsToUuidV7Helper
    {
        /// <summary>
        /// Re-creates every foreign key saved (and dropped) by <see cref="ExecutePreMigration"/>.
        /// Must run after all column type changes so parent and child key types match again.
        /// </summary>
        public static void ExecutePostMigration(MigrationBuilder mb)
        {
            mb.Sql(@"
DO $BODY$
DECLARE r record;
BEGIN
    IF to_regclass('pg_temp.__uuid_fk_backup') IS NOT NULL THEN
        FOR r IN SELECT tbl, conname, def FROM pg_temp.__uuid_fk_backup LOOP
            EXECUTE format('ALTER TABLE %s ADD CONSTRAINT %I %s', r.tbl, r.conname, r.def);
        END LOOP;
        DROP TABLE pg_temp.__uuid_fk_backup;
    END IF;
END $BODY$;");
        }

        public static void ExecutePreMigration(MigrationBuilder mb, IModel? model = null)
        {
            // Postgres rejects a type change on a key column while a foreign key links it to a column of a
            // different type, so save and drop every FK first; they are re-created by ExecutePostMigration.
            mb.Sql(@"
DROP TABLE IF EXISTS pg_temp.__uuid_fk_backup;
CREATE TEMP TABLE __uuid_fk_backup AS
    SELECT conrelid::regclass::text AS tbl, conname::text AS conname, pg_get_constraintdef(oid) AS def
    FROM pg_constraint
    WHERE contype = 'f' AND connamespace = current_schema()::regnamespace;
DO $BODY$
DECLARE r record;
BEGIN
    FOR r IN SELECT tbl, conname FROM pg_temp.__uuid_fk_backup LOOP
        EXECUTE format('ALTER TABLE %s DROP CONSTRAINT %I', r.tbl, r.conname);
    END LOOP;
END $BODY$;");

            var tablesWithId = new[]
            {
                "ActionCommands", "ActionKeywords", "Actions", "Aliases", "AudioCommands", "AutoShoutouts",
                "BannedSongs", "BannedViewers", "Cooldowns", "Counters", "DeathCounters", "DefaultCommands",
                "DiscordEvents", "ExternalCommands", "FishCatches", "FishCategories", "FishTypes", "FishingGolds",
                "FishingRepairEvents", "FishingSettings", "FishingShopItems", "FishingSnapEvents", "FishingTournamentCatches",
                "FishingTournamentEligibleCategories", "FishingTournamentFishTypes", "FishingTournamentRewardRules",
                "FishingTournaments", "GameSettings", "GiveawayEntries", "GiveawayExclusions", "GiveawayWinners",
                "GlobalVariables", "Keywords", "KnownBots", "Playlists", "PointCommands", "PointTypes",
                "QueueConfigurations", "Quotes", "RaidHistory", "RegisteredVoices", "Settings", "SongCooldowns",
                "SongRequestViewItems", "Songs", "SubscriptionHistories", "TimerGroups", "Triggers", "UserFishingBoosts",
                "UserPoints", "UserRegisteredVoices", "UserThemePreferences", "ViewerMessageCounts", "Viewers",
                "ViewersTime", "WheelProperties", "Wheels", "WordFilters", "obs_connections", "overlay_layouts",
                "overlay_widgets", "starship_expedition_history"
            };

            foreach (var table in tablesWithId)
            {
                ConvertColumn(mb, table, "Id");
            }

            var subactionTables = new[]
            {
                "subactions_alert", "subactions_balloon_ascent", "subactions_balloon_hazards", "subactions_balloon_start_session",
                "subactions_balloon_stop_session", "subactions_break", "subactions_channelpointsetenabledstate",
                "subactions_channelpointsetpausedstate", "subactions_checkpoints", "subactions_curling_slide",
                "subactions_curling_start_session", "subactions_curling_stop_session", "subactions_currenttime",
                "subactions_delay", "subactions_executeaction", "subactions_executecommand", "subactions_executedefaultcommand",
                "subactions_externalapi", "subactions_fishing", "subactions_fishinggiveitemtoplayer", "subactions_fishingmodify",
                "subactions_fishingtournamenteligiblecatch", "subactions_fishingtournamentend", "subactions_fishingtournamentstart",
                "subactions_followage", "subactions_foreachviewer", "subactions_getglobalvariable", "subactions_giftpoints",
                "subactions_giveawayprize", "subactions_golf_putt", "subactions_golf_start_session", "subactions_golf_stop_session",
                "subactions_logic_if_else", "subactions_managepoints", "subactions_multicounter",
                "subactions_obs_setbrowsersourceurl", "subactions_obs_setcolorsourcecolor", "subactions_obs_setimagesourcefile",
                "subactions_obs_setinputmute", "subactions_obs_setmediasourcefile", "subactions_obs_setmediastate",
                "subactions_obs_setscene", "subactions_obs_setscenefilterstate", "subactions_obs_setsourceaudiotrackstate",
                "subactions_obs_setsourcefilterstate", "subactions_obs_setsourcevisibility", "subactions_obs_settext",
                "subactions_obs_triggerhotkey", "subactions_openai", "subactions_overlay_timer_addtime",
                "subactions_overlay_timer_removetime", "subactions_overlay_timer_start", "subactions_overlay_timer_stop",
                "subactions_parachute_drop", "subactions_parachute_droplets", "subactions_parachute_rockets",
                "subactions_parachute_start_session", "subactions_parachute_stop_session", "subactions_playsound",
                "subactions_plinko_drop", "subactions_plinko_start_session", "subactions_plinko_stop_session",
                "subactions_pointcommand", "subactions_racing_join", "subactions_racing_start_session",
                "subactions_racing_stop_session", "subactions_raffleend", "subactions_raffleenter",
                "subactions_rafflegetentrycount", "subactions_rafflesettotalaward", "subactions_rafflesetwinnercount",
                "subactions_rafflestart", "subactions_randomint", "subactions_replytomessage", "subactions_resetcooldowns",
                "subactions_selectrandomviewers", "subactions_sendmessage", "subactions_setglobalvariable",
                "subactions_setvariable", "subactions_starship_show_event", "subactions_starship_show_status",
                "subactions_starship_start_expedition", "subactions_starship_stop_expedition", "subactions_start_drop_game_session",
                "subactions_stop_drop_game_session", "subactions_timergroupsetenabled", "subactions_togglecommanddisabled",
                "subactions_tts", "subactions_ufo_claw_control", "subactions_uptime", "subactions_watchtime", "subactions_writefile"
            };

            foreach (var table in subactionTables)
            {
                ConvertColumn(mb, table, "Id");
                ConvertColumn(mb, table, "ActionTypeId");
                ConvertColumn(mb, table, "CatchActionTypeId");
            }

            var fks = new (string Table, string Column)[]
            {
                ("Triggers", "ActionId"), ("Triggers", "CommandId"), ("Triggers", "DefaultCommandId"), ("Triggers", "KeywordId"), ("Triggers", "TimerGroupId"),
                ("subactions_executeaction", "ActionId"), ("subactions_foreachviewer", "ActionId"), ("subactions_timergroupsetenabled", "TimerGroupId"),
                ("UserPoints", "PointTypeId"), ("PointCommands", "PointTypeId"), ("Keywords", "PointTypeId"),
                ("ActionCommands", "PointTypeId"), ("ActionKeywords", "PointTypeId"), ("AudioCommands", "PointTypeId"),
                ("DefaultCommands", "PointTypeId"), ("ExternalCommands", "PointTypeId"),
                ("FishCategories", "FishTypeId"), ("FishCatches", "FishTypeId"), ("FishingShopItems", "TargetFishTypeId"),
                ("UserFishingBoosts", "ShopItemId"), ("FishingRepairEvents", "ShopItemId"), ("FishingRepairEvents", "UserBoostId"),
                ("FishingTournaments", "EntryFeePointTypeId"),
                ("FishingTournamentFishTypes", "FishingTournamentId"), ("FishingTournamentFishTypes", "FishTypeId"),
                ("FishingTournamentEligibleCategories", "FishingTournamentId"),
                ("FishingTournamentRewardRules", "FishingTournamentId"), ("FishingTournamentRewardRules", "PointTypeId"), ("FishingTournamentRewardRules", "TargetFishTypeId"),
                ("FishingTournamentCatches", "FishingTournamentId"), ("FishingTournamentCatches", "FishTypeId"), ("FishingTournamentCatches", "FishCatchId"),
                ("WheelProperties", "WheelId")
            };

            foreach (var (table, col) in fks)
            {
                ConvertColumn(mb, table, col);
            }

            // Also cover any Guid key column the hardcoded lists miss (e.g. Songs.MusicPlaylistId).
            // ConvertColumn skips columns that are already uuid, so repeats are harmless.
            if (model != null)
            {
                foreach (var (table, col) in GetGuidKeyColumns(model))
                {
                    ConvertColumn(mb, table, col);
                }
            }
        }

        private static IEnumerable<(string Table, string Column)> GetGuidKeyColumns(IModel model)
        {
            var result = new HashSet<(string, string)>();
            foreach (var entityType in model.GetEntityTypes())
            {
                var table = entityType.GetTableName();
                if (table == null || entityType.GetViewName() != null)
                {
                    continue;
                }

                var storeObject = StoreObjectIdentifier.Table(table, entityType.GetSchema());
                // All Guid properties, not only keys: unconstrained references such as OBSConnectionId hold legacy ints too.
                foreach (var p in entityType.GetProperties())
                {
                    var clr = Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType;
                    if (clr == typeof(Guid) && p.GetColumnName(storeObject) is { } column)
                    {
                        result.Add((table, column));
                    }
                }
            }

            return result;
        }

        private static void ConvertColumn(MigrationBuilder mb, string table, string col)
        {
            var sql = $@"
DO $BODY$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = current_schema() AND lower(table_name) = lower('{table}') AND lower(column_name) = lower('{col}')
    ) THEN
        BEGIN
            ALTER TABLE ""{table}"" ALTER COLUMN ""{col}"" DROP IDENTITY IF EXISTS;
        EXCEPTION WHEN OTHERS THEN
            NULL;
        END;

        IF EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = current_schema() AND lower(table_name) = lower('{table}') AND lower(column_name) = lower('{col}') AND data_type != 'uuid'
        ) THEN
            -- An integer default (e.g. 0) cannot be cast to uuid automatically.
            ALTER TABLE ""{table}"" ALTER COLUMN ""{col}"" DROP DEFAULT;
            ALTER TABLE ""{table}"" ALTER COLUMN ""{col}"" TYPE uuid USING (
                CASE
                    WHEN ""{col}"" IS NULL THEN NULL
                    WHEN length(""{col}""::text) < 36 THEN ('00000000-0000-7000-8000-' || lpad(to_hex(""{col}""::bigint), 12, '0'))::uuid
                    ELSE ""{col}""::text::uuid
                END
            );
        END IF;
    END IF;
END $BODY$;";
            mb.Sql(sql);
        }
    }
}

