using PenguinTwitchBot.Models.Themes;

namespace PenguinTwitchBot.Services;

public interface ICustomThemeService
{
    Task<List<CustomThemeModel>> GetThemesAsync();
    Task<CustomThemeModel?> GetThemeByIdAsync(string id);
    Task SaveThemeAsync(CustomThemeModel theme);
    Task DeleteThemeAsync(string id);
    Task SetDefaultThemeAsync(string id);
    Task SetThemeEnabledAsync(string id, bool isEnabled);
    Task ResetToDefaultsAsync();
    event EventHandler? ThemesChanged;
}

