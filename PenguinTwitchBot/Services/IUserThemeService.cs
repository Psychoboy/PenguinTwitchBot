using MudBlazor;
using Microsoft.JSInterop;
using PenguinTwitchBot.Models.Themes;

namespace PenguinTwitchBot.Services;

public interface IUserThemeService : IDisposable
{
    bool IsDarkMode { get; }
    string CurrentThemeId { get; }
    MudTheme CurrentTheme { get; }
    CustomThemeModel? CurrentThemeModel { get; }
    IReadOnlyList<CustomThemeModel> AvailableThemes { get; }
    bool IsInitialized { get; }

    event Action? OnThemeChanged;

    Task InitializeAsync(IJSRuntime jsRuntime);
    Task ToggleDarkModeAsync(IJSRuntime jsRuntime);
    Task SetDarkModeAsync(bool isDark, IJSRuntime jsRuntime);
    Task SelectThemeAsync(string themeId, IJSRuntime jsRuntime);
    Task RefreshThemesAsync();
}

