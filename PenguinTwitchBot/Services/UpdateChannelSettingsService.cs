using Microsoft.Extensions.DependencyInjection;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

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
            var newSetting = new Setting
            {
                Name = IncludePreviewSettingName,
                DataType = Setting.DataTypeEnum.Int,
                IntSetting = defaultValue ? 1 : 0
            };
            await db.Settings.AddAsync(newSetting);
            await db.SaveChangesAsync();
            return defaultValue;
        }

        return setting.IntSetting != 0;
    }

    public async Task SetIncludePreviewReleasesAsync(bool value)
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

        await db.SaveChangesAsync();
    }
}
