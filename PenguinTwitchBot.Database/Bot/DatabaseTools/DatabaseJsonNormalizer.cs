using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Core.Database;

namespace PenguinTwitchBot.Database.Bot.DatabaseTools
{
    /// <summary>
    /// Utility to inspect and normalize embedded JSON payloads (e.g. Triggers.Configuration,
    /// LogicIfElseType TrueSubActions/FalseSubActions, Settings) during startup migration.
    /// Converts legacy integer identifiers into canonical RFC 9562 UUIDv7 strings.
    /// </summary>
    public static class DatabaseJsonNormalizer
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new LegacyIntCompatibleGuidJsonConverter(),
                new LegacyIntCompatibleNullableGuidJsonConverter()
            }
        };

        public static async Task NormalizeAsync(ApplicationDbContext context, ILogger? logger = null)
        {
            try
            {
                // 1. Triggers Configuration
                var triggers = await context.Triggers.ToListAsync();
                bool triggersChanged = false;
                foreach (var trigger in triggers)
                {
                    if (string.IsNullOrWhiteSpace(trigger.Configuration))
                        continue;

                    try
                    {
                        using var doc = JsonDocument.Parse(trigger.Configuration);
                        if (doc.RootElement.ValueKind != JsonValueKind.Object)
                            continue;

                        var dict = new Dictionary<string, object?>();
                        bool modified = false;

                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            if ((prop.NameEquals("CommandId") || prop.NameEquals("KeywordId") ||
                                 prop.NameEquals("TimerGroupId") || prop.NameEquals("ActionId")) &&
                                prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt64(out var legacyId))
                            {
                                dict[prop.Name] = UuidMigrationHelper.FromLegacyInt(legacyId).ToString();
                                modified = true;
                            }
                            else
                            {
                                dict[prop.Name] = prop.Value.Clone();
                            }
                        }

                        if (modified)
                        {
                            trigger.Configuration = JsonSerializer.Serialize(dict, JsonOptions);
                            triggersChanged = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to normalize configuration for Trigger {Id}", trigger.Id);
                    }
                }

                if (triggersChanged)
                {
                    await context.SaveChangesAsync();
                }

                // 2. LogicIfElseType Subactions
                var logicIfElseList = await context.Set<LogicIfElseType>().ToListAsync();
                bool logicChanged = false;
                foreach (var item in logicIfElseList)
                {
                    bool itemUpdated = false;
                    if (item.TrueSubActions.Count > 0)
                    {
                        foreach (var sub in item.TrueSubActions)
                        {
                            if (sub.Id == Guid.Empty)
                            {
                                sub.Id = Guid.CreateVersion7();
                                itemUpdated = true;
                            }
                        }
                    }
                    if (item.FalseSubActions.Count > 0)
                    {
                        foreach (var sub in item.FalseSubActions)
                        {
                            if (sub.Id == Guid.Empty)
                            {
                                sub.Id = Guid.CreateVersion7();
                                itemUpdated = true;
                            }
                        }
                    }
                    if (itemUpdated)
                    {
                        logicChanged = true;
                    }
                }

                if (logicChanged)
                {
                    await context.SaveChangesAsync();
                }

                // 3. Settings table
                var settings = await context.Settings.ToListAsync();
                bool settingsChanged = false;
                foreach (var s in settings)
                {
                    if (s.Name == "RaidRewardPointTypeId" && s.IntSetting > 0 && string.IsNullOrWhiteSpace(s.StringSetting))
                    {
                        s.StringSetting = UuidMigrationHelper.FromLegacyInt(s.IntSetting).ToString();
                        s.DataType = Bot.Models.Setting.DataTypeEnum.String;
                        settingsChanged = true;
                    }
                    else if (s.Name == "LeaderboardsLayout" && !string.IsNullOrWhiteSpace(s.StringSetting))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(s.StringSetting);
                            // If it parses fine and has converters, it's compatible
                        }
                        catch
                        {
                            // Ignore malformed setting
                        }
                    }
                }

                if (settingsChanged)
                {
                    await context.SaveChangesAsync();
                }

                // 4. Timer triggers used to be named "TimerGroup_{id}"; name them after their group instead.
                var legacyTimerTriggers = await context.Triggers
                    .Where(t => t.Type == Bot.Models.Actions.Triggers.TriggerTypes.Timer
                        && t.TimerGroupId != null
                        && t.Name.StartsWith("TimerGroup_"))
                    .ToListAsync();
                if (legacyTimerTriggers.Count > 0)
                {
                    var groupNames = await context.TimerGroups.ToDictionaryAsync(g => g.Id, g => g.Name);
                    foreach (var trigger in legacyTimerTriggers)
                    {
                        if (groupNames.TryGetValue(trigger.TimerGroupId!.Value, out var groupName) && !string.IsNullOrWhiteSpace(groupName))
                        {
                            trigger.Name = groupName;
                        }
                    }
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "DatabaseJsonNormalizer encountered an issue during startup normalization.");
            }
        }
    }
}

