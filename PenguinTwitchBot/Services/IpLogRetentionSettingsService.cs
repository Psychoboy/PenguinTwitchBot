using Microsoft.Extensions.DependencyInjection;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Services;

public interface IIpLogRetentionSettingsService
{
    Task<int> GetIpLogMonthsToKeepAsync() => GetIpLogMonthsToKeepAsync(6);
    Task<int> GetIpLogMonthsToKeepAsync(int defaultValue);
    Task SetIpLogMonthsToKeepAsync(int value);
}

public class IpLogRetentionSettingsService(IServiceScopeFactory scopeFactory) : IIpLogRetentionSettingsService
{
    private const string SettingName = "IpLogMonthsToKeep";

    public async Task<int> GetIpLogMonthsToKeepAsync(int defaultValue)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await db.Settings.GetAsync(x => x.Name == SettingName)).FirstOrDefault();
        if (setting == null)
        {
            setting = new Setting
            {
                Name = SettingName,
                DataType = Setting.DataTypeEnum.Int,
                IntSetting = defaultValue
            };
            await db.Settings.AddAsync(setting);
            await db.SaveChangesAsync();
        }

        return setting.IntSetting;
    }

    public async Task SetIpLogMonthsToKeepAsync(int value)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await db.Settings.GetAsync(x => x.Name == SettingName)).FirstOrDefault();

        if (setting == null)
        {
            setting = new Setting
            {
                Name = SettingName,
                DataType = Setting.DataTypeEnum.Int,
                IntSetting = value
            };
            await db.Settings.AddAsync(setting);
        }
        else
        {
            setting.DataType = Setting.DataTypeEnum.Int;
            setting.IntSetting = value;
            db.Settings.Update(setting);
        }

        await db.SaveChangesAsync();
    }
}
