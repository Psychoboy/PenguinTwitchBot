using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;
using Npgsql;
using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Services;

public interface IUpdateChannelSettingsService
{
    Task<bool> GetIncludePreviewReleasesAsync(bool defaultValue = false);
    Task SetIncludePreviewReleasesAsync(bool value);
}

public class UpdateChannelSettingsService(IServiceScopeFactory scopeFactory) : IUpdateChannelSettingsService
{
    private const string IncludePreviewSettingName = "Updates.IncludePreviewReleases";

    public async Task<bool> GetIncludePreviewReleasesAsync(bool defaultValue = false)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await db.Settings.GetAsync(x => x.Name == IncludePreviewSettingName)).FirstOrDefault();
        if (setting is null)
        {
            return defaultValue;
        }

        return setting.IntSetting != 0;
    }

    public async Task SetIncludePreviewReleasesAsync(bool value)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var setting = (await db.Settings.GetAsync(x => x.Name == IncludePreviewSettingName)).FirstOrDefault();
            if (setting is null)
            {
                setting = new Setting
                {
                    Name = IncludePreviewSettingName,
                    DataType = Setting.DataTypeEnum.Int,
                    IntSetting = value ? 1 : 0
                };
                await db.Settings.AddAsync(setting);
            }
            else
            {
                setting.DataType = Setting.DataTypeEnum.Int;
                setting.IntSetting = value ? 1 : 0;
                db.Settings.Update(setting);
            }

            try
            {
                await db.SaveChangesAsync();
                return;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex) && attempt == 0)
            {
                // Do Nothing
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
