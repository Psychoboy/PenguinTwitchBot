using Microsoft.Extensions.DependencyInjection;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Services;

public interface IBackupSettingsService
{
    Task<int> GetBackupCountToKeepAsync() => GetBackupCountToKeepAsync(15);
    Task<int> GetBackupDaysToKeepAsync() => GetBackupDaysToKeepAsync(15);
    Task<int> GetBackupCountToKeepAsync(int defaultValue);
    Task<int> GetBackupDaysToKeepAsync(int defaultValue);
    Task SetBackupCountToKeepAsync(int value);
    Task SetBackupDaysToKeepAsync(int value);
}

public class BackupSettingsService(IServiceScopeFactory scopeFactory) : IBackupSettingsService
{
    public async Task<int> GetBackupCountToKeepAsync(int defaultValue)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await db.Settings.GetAsync(x => x.Name == "BackupCountToKeep")).FirstOrDefault();
        if (setting == null)
        {
            var newSetting = new Setting
            {
                Name = "BackupCountToKeep",
                DataType = Setting.DataTypeEnum.Int,
                IntSetting = defaultValue
            };
            await db.Settings.AddAsync(newSetting);
            await db.SaveChangesAsync();
        }
        return setting?.IntSetting ?? defaultValue;
    }

    public async Task<int> GetBackupDaysToKeepAsync(int defaultValue)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await db.Settings.GetAsync(x => x.Name == "BackupDaysToKeep")).FirstOrDefault();
        if (setting == null)
        {
            var newSetting = new Setting
            {
                Name = "BackupDaysToKeep",
                DataType = Setting.DataTypeEnum.Int,
                IntSetting = defaultValue
            };
            await db.Settings.AddAsync(newSetting);
            await db.SaveChangesAsync();
        }
        return setting?.IntSetting ?? defaultValue;
    }

    public async Task SetBackupCountToKeepAsync(int value)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await db.Settings.GetAsync(x => x.Name == "BackupCountToKeep")).FirstOrDefault();
        if (setting == null)
        {
            var newSetting = new Setting
            {
                Name = "BackupCountToKeep",
                DataType = Setting.DataTypeEnum.Int,
                IntSetting = value
            };
            await db.Settings.AddAsync(newSetting);
        }
        else
        {
            setting.IntSetting = value;
            db.Settings.Update(setting);
        }
        await db.SaveChangesAsync();
    }

    public async Task SetBackupDaysToKeepAsync(int value)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await db.Settings.GetAsync(x => x.Name == "BackupDaysToKeep")).FirstOrDefault();
        if (setting == null)
        {
            var newSetting = new Setting
            {
                Name = "BackupDaysToKeep",
                DataType = Setting.DataTypeEnum.Int,
                IntSetting = value
            };
            await db.Settings.AddAsync(newSetting);
        }
        else
        {
            setting.IntSetting = value;
            db.Settings.Update(setting);
        }
        await db.SaveChangesAsync();
    }
}
