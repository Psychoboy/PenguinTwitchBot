using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Models.Themes;

namespace PenguinTwitchBot.Services;

public sealed class CustomThemeService(IServiceScopeFactory scopeFactory) : ICustomThemeService
{
    private const string SettingName = "CustomThemes";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public event EventHandler? ThemesChanged;

    public async Task<List<CustomThemeModel>> GetThemesAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingName)).FirstOrDefault();

        if (setting == null || string.IsNullOrWhiteSpace(setting.StringSetting))
        {
            var presets = PresetThemes.GetPresets();
            await SaveThemesInternalAsync(unitOfWork, presets);
            return presets;
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<CustomThemeModel>>(setting.StringSetting, JsonOptions);
            if (list == null || list.Count == 0)
            {
                var presets = PresetThemes.GetPresets();
                await SaveThemesInternalAsync(unitOfWork, presets);
                return presets;
            }

            // Ensure there is at least one default
            if (!list.Any(x => x.IsDefault))
            {
                var first = list.FirstOrDefault(x => x.Id == PresetThemes.DefaultThemeId) ?? list.First();
                first.IsDefault = true;
            }

            // Ensure all default themes are enabled
            foreach (var t in list.Where(x => x.IsDefault))
            {
                t.IsEnabled = true;
            }

            // Normalize colors so any unrounded rgba values from previous saves are clean hex
            foreach (var t in list)
            {
                t.LightPalette.NormalizeColors();
                t.DarkPalette.NormalizeColors();
            }

            return list;
        }
        catch
        {
            var presets = PresetThemes.GetPresets();
            return presets;
        }
    }

    public async Task<CustomThemeModel?> GetThemeByIdAsync(string id)
    {
        var themes = await GetThemesAsync();
        return themes.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task SaveThemeAsync(CustomThemeModel theme)
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var themes = await GetThemesAsync();

        var existingIndex = themes.FindIndex(x => string.Equals(x.Id, theme.Id, StringComparison.OrdinalIgnoreCase));

        if (theme.IsDefault)
        {
            theme.IsEnabled = true;
            foreach (var t in themes)
            {
                t.IsDefault = false;
            }
        }

        if (existingIndex >= 0)
        {
            themes[existingIndex] = theme;
        }
        else
        {
            themes.Add(theme);
        }

        // If no default exists, set this one or first
        if (!themes.Any(x => x.IsDefault))
        {
            themes.First().IsDefault = true;
            themes.First().IsEnabled = true;
        }

        await SaveThemesInternalAsync(unitOfWork, themes);
        ThemesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteThemeAsync(string id)
    {
        if (string.Equals(id, PresetThemes.DefaultThemeId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot delete the default MudBlazor theme.");
        }

        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var themes = await GetThemesAsync();

        var existing = themes.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
        if (existing == null) return;

        bool wasDefault = existing.IsDefault;
        themes.Remove(existing);

        if (wasDefault && themes.Count > 0)
        {
            var newDefault = themes.FirstOrDefault(x => x.Id == PresetThemes.DefaultThemeId) ?? themes.First();
            newDefault.IsDefault = true;
            newDefault.IsEnabled = true;
        }

        await SaveThemesInternalAsync(unitOfWork, themes);
        ThemesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetDefaultThemeAsync(string id)
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var themes = await GetThemesAsync();

        var target = themes.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
        if (target == null) return;

        foreach (var t in themes)
        {
            var isTarget = string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase);
            t.IsDefault = isTarget;
            if (isTarget)
            {
                t.IsEnabled = true;
            }
        }

        await SaveThemesInternalAsync(unitOfWork, themes);
        ThemesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetThemeEnabledAsync(string id, bool isEnabled)
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var themes = await GetThemesAsync();

        var target = themes.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
        if (target == null) return;

        if (target.IsDefault && !isEnabled)
        {
            throw new InvalidOperationException("Cannot disable the active default theme.");
        }

        target.IsEnabled = isEnabled;

        await SaveThemesInternalAsync(unitOfWork, themes);
        ThemesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task ResetToDefaultsAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var presets = PresetThemes.GetPresets();

        await SaveThemesInternalAsync(unitOfWork, presets);
        ThemesChanged?.Invoke(this, EventArgs.Empty);
    }

    private static async Task SaveThemesInternalAsync(IUnitOfWork unitOfWork, List<CustomThemeModel> themes)
    {
        var setting = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingName)).FirstOrDefault();
        var json = JsonSerializer.Serialize(themes, JsonOptions);

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
}

