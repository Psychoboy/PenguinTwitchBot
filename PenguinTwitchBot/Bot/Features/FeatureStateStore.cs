using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Features
{
    public interface IFeatureStateStore
    {
        Task<bool> GetEnabledAsync(string featureKey, bool defaultValue = true);
        Task SetEnabledAsync(string featureKey, bool value);
    }

    public sealed class FeatureStateStore(IServiceScopeFactory scopeFactory) : IFeatureStateStore
    {
        private const int MaxSaveRetries = 5;

        public async Task<bool> GetEnabledAsync(string featureKey, bool defaultValue = true)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var setting = (await db.Settings.GetAsync(x => x.Name == BuildEnabledSettingName(featureKey))).FirstOrDefault();
            return setting == null ? defaultValue : setting.IntSetting != 0;
        }

        public async Task SetEnabledAsync(string featureKey, bool value)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var settingName = BuildEnabledSettingName(featureKey);
            var setting = (await db.Settings.GetAsync(x => x.Name == settingName)).FirstOrDefault();

            if (setting == null)
            {
                setting = new Setting
                {
                    Name = settingName,
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

            await SaveChangesWithRetryAsync(db);
        }

        private static string BuildEnabledSettingName(string featureKey)
        {
            return $"FeatureService.{featureKey}.Enabled";
        }

        private static async Task SaveChangesWithRetryAsync(IUnitOfWork db)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await db.SaveChangesAsync();
                    return;
                }
                catch (DbUpdateException ex) when (IsSqliteTableLock(ex) && attempt < MaxSaveRetries)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt));
                }
            }
        }

        private static bool IsSqliteTableLock(DbUpdateException ex)
        {
            return ex.InnerException is SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 6;
        }
    }
}