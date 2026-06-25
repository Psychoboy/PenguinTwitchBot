using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Services;

public sealed class HomepageLayoutService(IServiceScopeFactory scopeFactory)
{
    private const string SettingName = "HomepageLayout";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<HomepageLayoutConfig> GetLayoutAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingName)).FirstOrDefault();

        if (setting == null || string.IsNullOrWhiteSpace(setting.StringSetting))
        {
            return HomepageWidgetCatalog.CreateDefaultLayout();
        }

        try
        {
            return HomepageWidgetCatalog.Normalize(JsonSerializer.Deserialize<HomepageLayoutConfig>(setting.StringSetting, JsonOptions));
        }
        catch
        {
            return HomepageWidgetCatalog.CreateDefaultLayout();
        }
    }

    public async Task SaveLayoutAsync(HomepageLayoutConfig layout)
    {
        layout = HomepageWidgetCatalog.Normalize(layout);

        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingName)).FirstOrDefault();
        var json = JsonSerializer.Serialize(layout, JsonOptions);

        if (setting == null)
        {
            setting = new Setting
            {
                Name = SettingName,
                DataType = Setting.DataTypeEnum.String,
                StringSetting = json
            };
            unitOfWork.Settings.Add(setting);
        }
        else
        {
            setting.DataType = Setting.DataTypeEnum.String;
            setting.StringSetting = json;
            unitOfWork.Settings.Update(setting);
        }

        await unitOfWork.SaveChangesAsync();
    }

    public async Task ResetLayoutAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingName)).FirstOrDefault();

        if (setting == null)
        {
            return;
        }

        unitOfWork.Settings.Remove(setting);
        await unitOfWork.SaveChangesAsync();
    }
}