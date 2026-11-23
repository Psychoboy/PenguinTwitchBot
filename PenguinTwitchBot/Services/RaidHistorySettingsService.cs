using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Services;

public interface IRaidHistorySettingsService
{
    Task<bool> GetShowLiveTeamMembersAsync();
    Task SetShowLiveTeamMembersAsync(bool enabled);
}

/// <summary>
/// Persists Raid History display preferences in the Settings table.
/// Booleans are stored as Int (0/1) since the Setting model has no bool type.
/// </summary>
public class RaidHistorySettingsService(IServiceScopeFactory scopeFactory) : IRaidHistorySettingsService
{
    public const string ShowLiveTeamMembersSettingName = "RaidHistory.ShowLiveTeamMembers";

    public async Task<bool> GetShowLiveTeamMembersAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await db.Settings.GetAsync(x => x.Name == ShowLiveTeamMembersSettingName)).FirstOrDefault();
        if (setting is null)
        {
            return true;
        }

        return setting.IntSetting != 0;
    }

    public async Task SetShowLiveTeamMembersAsync(bool enabled)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var setting = (await db.Settings.GetAsync(x => x.Name == ShowLiveTeamMembersSettingName)).FirstOrDefault();
            if (setting is null)
            {
                setting = new Setting
                {
                    Name = ShowLiveTeamMembersSettingName,
                    DataType = Setting.DataTypeEnum.Int,
                    IntSetting = enabled ? 1 : 0
                };
                await db.Settings.AddAsync(setting);
            }
            else
            {
                setting.DataType = Setting.DataTypeEnum.Int;
                setting.IntSetting = enabled ? 1 : 0;
                db.Settings.Update(setting);
            }

            try
            {
                await db.SaveChangesAsync();
                return;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex) && attempt == 0)
            {
                // Retry once if another request inserted the setting concurrently.
            }
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        if (ex.InnerException is SqliteException sqliteException && sqliteException.SqliteExtendedErrorCode == 2067)
        {
            return true;
        }

        if (ex.InnerException is PostgresException postgresException && postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return true;
        }

        return false;
    }
}
