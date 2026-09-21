using Microsoft.JSInterop;
using MudBlazor;
using PenguinTwitchBot.Models.Themes;

namespace PenguinTwitchBot.Services;

public sealed class UserThemeService : IUserThemeService
{
    private readonly ICustomThemeService _customThemeService;
    private List<CustomThemeModel> _availableThemes = [];
    private bool _disposed;

    public bool IsDarkMode { get; private set; } = true;
    public string CurrentThemeId { get; private set; } = PresetThemes.DefaultThemeId;
    public MudTheme CurrentTheme { get; private set; } = new();
    public CustomThemeModel? CurrentThemeModel => _availableThemes.FirstOrDefault(x => string.Equals(x.Id, CurrentThemeId, StringComparison.OrdinalIgnoreCase));
    public IReadOnlyList<CustomThemeModel> AvailableThemes => _availableThemes;
    public bool IsInitialized { get; private set; }

    public event Action? OnThemeChanged;

    public UserThemeService(ICustomThemeService customThemeService)
    {
        _customThemeService = customThemeService;
        _customThemeService.ThemesChanged += HandleThemesChanged;
    }

    public async Task InitializeAsync(IJSRuntime jsRuntime)
    {
        if (IsInitialized) return;

        try
        {
            var allThemes = await _customThemeService.GetThemesAsync();
            _availableThemes = allThemes.Where(x => x.IsEnabled).ToList();
            if (_availableThemes.Count == 0)
            {
                _availableThemes = allThemes;
            }

            var defaultTheme = _availableThemes.FirstOrDefault(x => x.IsDefault)
                ?? _availableThemes.FirstOrDefault(x => x.Id == PresetThemes.DefaultThemeId)
                ?? _availableThemes.FirstOrDefault();

            if (defaultTheme != null)
            {
                CurrentThemeId = defaultTheme.Id;
                CurrentTheme = defaultTheme.ToMudTheme();
            }

            try
            {
                var pref = await jsRuntime.InvokeAsync<UserThemePreference?>("penguinTheme.getPreference");
                if (pref != null)
                {
                    IsDarkMode = pref.IsDarkMode;

                    if (!string.IsNullOrWhiteSpace(pref.ThemeId) && _availableThemes.Any(x => string.Equals(x.Id, pref.ThemeId, StringComparison.OrdinalIgnoreCase)))
                    {
                        CurrentThemeId = pref.ThemeId;
                        var matched = _availableThemes.First(x => string.Equals(x.Id, pref.ThemeId, StringComparison.OrdinalIgnoreCase));
                        CurrentTheme = matched.ToMudTheme();
                    }
                }
            }
            catch (Exception)
            {
                // Prerender or JS not ready, ignore
            }

            IsInitialized = true;
            OnThemeChanged?.Invoke();
        }
        catch (Exception)
        {
            // Fallback to default
            CurrentTheme = new MudTheme();
            IsInitialized = true;
            OnThemeChanged?.Invoke();
        }
    }

    public async Task ToggleDarkModeAsync(IJSRuntime jsRuntime)
    {
        await SetDarkModeAsync(!IsDarkMode, jsRuntime);
    }

    public async Task SetDarkModeAsync(bool isDark, IJSRuntime jsRuntime)
    {
        IsDarkMode = isDark;
        OnThemeChanged?.Invoke();

        try
        {
            await jsRuntime.InvokeVoidAsync("penguinTheme.setPreference", IsDarkMode, CurrentThemeId);
        }
        catch (Exception)
        {
            // JS exception ignored (e.g. disconnected circuit)
        }
    }

    public async Task SelectThemeAsync(string themeId, IJSRuntime jsRuntime)
    {
        var matched = _availableThemes.FirstOrDefault(x => string.Equals(x.Id, themeId, StringComparison.OrdinalIgnoreCase));
        if (matched != null)
        {
            CurrentThemeId = matched.Id;
            CurrentTheme = matched.ToMudTheme();
        }
        else
        {
            CurrentThemeId = PresetThemes.DefaultThemeId;
            CurrentTheme = new MudTheme();
        }

        OnThemeChanged?.Invoke();

        try
        {
            await jsRuntime.InvokeVoidAsync("penguinTheme.setPreference", IsDarkMode, CurrentThemeId);
        }
        catch (Exception)
        {
            // JS exception ignored
        }
    }

    public async Task RefreshThemesAsync()
    {
        var allThemes = await _customThemeService.GetThemesAsync();
        _availableThemes = allThemes.Where(x => x.IsEnabled).ToList();
        if (_availableThemes.Count == 0)
        {
            _availableThemes = allThemes;
        }

        var matched = _availableThemes.FirstOrDefault(x => string.Equals(x.Id, CurrentThemeId, StringComparison.OrdinalIgnoreCase));
        if (matched != null)
        {
            CurrentTheme = matched.ToMudTheme();
        }
        else
        {
            var defaultTheme = _availableThemes.FirstOrDefault(x => x.IsDefault)
                ?? _availableThemes.FirstOrDefault(x => x.Id == PresetThemes.DefaultThemeId)
                ?? _availableThemes.FirstOrDefault();
            if (defaultTheme != null)
            {
                CurrentThemeId = defaultTheme.Id;
                CurrentTheme = defaultTheme.ToMudTheme();
            }
        }
        OnThemeChanged?.Invoke();
    }

    private async void HandleThemesChanged(object? sender, EventArgs e)
    {
        try
        {
            await RefreshThemesAsync();
        }
        catch
        {
            // Ignore background refresh errors
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _customThemeService.ThemesChanged -= HandleThemesChanged;
    }
}

