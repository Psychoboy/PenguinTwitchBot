using Microsoft.Extensions.DependencyInjection;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Services;

public interface IChatHistoryRetentionSettingsService
{
    Task<int> GetChatHistoryMonthsToKeepAsync() => GetChatHistoryMonthsToKeepAsync(12);
    Task<int> GetChatHistoryMonthsToKeepAsync(int defaultValue);
    Task SetChatHistoryMonthsToKeepAsync(int value);
}

public class ChatHistoryRetentionSettingsService(IServiceScopeFactory scopeFactory) : IChatHistoryRetentionSettingsService
{
    private const string SettingName = "ChatHistoryMonthsToKeep";

    public async Task<int> GetChatHistoryMonthsToKeepAsync(int defaultValue)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await db.Settings.GetAsync(x => x.Name == SettingName)).FirstOrDefault();
        if (setting == null)
        {
            var newSetting = new Setting
            {
                Name = SettingName,
                DataType = Setting.DataTypeEnum.Int,
                IntSetting = defaultValue
            };
            await db.Settings.AddAsync(newSetting);
            await db.SaveChangesAsync();
        }

        return setting?.IntSetting ?? defaultValue;
    }

    public async Task SetChatHistoryMonthsToKeepAsync(int value)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await db.Settings.GetAsync(x => x.Name == SettingName)).FirstOrDefault();
        if (setting == null)
        {
            var newSetting = new Setting
            {
                Name = SettingName,
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
