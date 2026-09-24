using PenguinTwitchBot.Models.Themes;

namespace PenguinTwitchBot.Services;

public interface ICustomThemeService
{
    Task<List<CustomThemeModel>> GetThemesAsync();
    IReadOnlyList<CustomThemeModel> GetCachedThemes();
    CustomThemeModel GetDefaultTheme();
    Task<CustomThemeModel?> GetThemeByIdAsync(string id);
    Task SaveThemeAsync(CustomThemeModel theme);
    Task DeleteThemeAsync(string id);
    Task SetDefaultThemeAsync(string id);
    Task SetThemeEnabledAsync(string id, bool isEnabled);
    Task ResetToDefaultsAsync();
    string ExportThemeToJson(CustomThemeModel theme);
    string ExportThemesToJson(IEnumerable<CustomThemeModel> themes);
    List<CustomThemeModel> ParseThemesFromJson(string json);
    Task<List<CustomThemeModel>> ImportThemesFromJsonAsync(string json, bool assignNewIds = true);
    event EventHandler? ThemesChanged;
}

