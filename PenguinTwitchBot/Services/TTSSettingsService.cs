using Microsoft.Extensions.DependencyInjection;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Services;

public class TTSSettingsService(IServiceScopeFactory scopeFactory) : ITTSSettingsService
{
    private const string KokoroThreadsSettingName = "KokoroThreads";

    public async Task<int> GetKokoroThreadsAsync(int defaultValue = 2)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await db.Settings.GetAsync(x => x.Name == KokoroThreadsSettingName)).FirstOrDefault();
        if (setting == null)
        {
            var newSetting = new Setting
            {
                Name = KokoroThreadsSettingName,
                DataType = Setting.DataTypeEnum.Int,
                IntSetting = defaultValue
            };
            await db.Settings.AddAsync(newSetting);
            await db.SaveChangesAsync();
            return defaultValue;
        }

        return setting.IntSetting > 0 ? setting.IntSetting : defaultValue;
    }

    public async Task SetKokoroThreadsAsync(int value)
    {
        var clamped = Math.Clamp(value, 1, Environment.ProcessorCount);
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await db.Settings.GetAsync(x => x.Name == KokoroThreadsSettingName)).FirstOrDefault();
        if (setting == null)
        {
            var newSetting = new Setting
            {
                Name = KokoroThreadsSettingName,
                DataType = Setting.DataTypeEnum.Int,
                IntSetting = clamped
            };
            await db.Settings.AddAsync(newSetting);
        }
        else
        {
            setting.IntSetting = clamped;
            db.Settings.Update(setting);
        }

        await db.SaveChangesAsync();
    }
}

