using System.Text.Json;
using Microsoft.AspNetCore.Http;
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

    public UserThemeService(ICustomThemeService customThemeService, IHttpContextAccessor? httpContextAccessor = null)
    {
        _customThemeService = customThemeService;
        _customThemeService.ThemesChanged += HandleThemesChanged;

        InitializeFromCache();

        if (httpContextAccessor?.HttpContext?.Request?.Cookies != null &&
            httpContextAccessor.HttpContext.Request.Cookies.TryGetValue("penguin_theme_pref", out var cookieVal) &&
            !string.IsNullOrWhiteSpace(cookieVal))
        {
            ApplyInitialPreference(cookieVal);
        }
    }

    private void InitializeFromCache()
    {
        try
        {
            var cached = _customThemeService.GetCachedThemes();
            if (cached != null && cached.Count > 0)
            {
                _availableThemes = cached.Where(x => x.IsEnabled).ToList();
                if (_availableThemes.Count == 0)
                {
                    _availableThemes = cached.ToList();
                }

                var defaultTheme = _customThemeService.GetDefaultTheme();
                CurrentThemeId = defaultTheme.Id;
                CurrentTheme = defaultTheme.ToMudTheme();
            }
        }
        catch
        {
            // If cache/db access fails, fallback to MudTheme
        }
    }

    public void ApplyInitialPreference(string? rawPreferenceOrCookie)
    {
        if (string.IsNullOrWhiteSpace(rawPreferenceOrCookie)) return;

        try
        {
            var decoded = rawPreferenceOrCookie.Contains('%') ? Uri.UnescapeDataString(rawPreferenceOrCookie) : rawPreferenceOrCookie;
            var pref = JsonSerializer.Deserialize<UserThemePreference>(decoded, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (pref != null)
            {
                IsDarkMode = pref.IsDarkMode;

                if (!string.IsNullOrWhiteSpace(pref.ThemeId))
                {
                    var matched = _availableThemes.FirstOrDefault(x => string.Equals(x.Id, pref.ThemeId, StringComparison.OrdinalIgnoreCase));
                    if (matched != null)
                    {
                        CurrentThemeId = matched.Id;
                        CurrentTheme = matched.ToMudTheme();
                    }
                }
            }
        }
        catch
        {
            // Ignore malformed cookie/preference
        }
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

            try
            {
                var pref = await jsRuntime.InvokeAsync<UserThemePreference?>("penguinTheme.getPreference");
                if (pref != null)
                {
                    bool changed = false;
                    if (IsDarkMode != pref.IsDarkMode)
                    {
                        IsDarkMode = pref.IsDarkMode;
                        changed = true;
                    }

                    if (!string.IsNullOrWhiteSpace(pref.ThemeId) && _availableThemes.Any(x => string.Equals(x.Id, pref.ThemeId, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!string.Equals(CurrentThemeId, pref.ThemeId, StringComparison.OrdinalIgnoreCase))
                        {
                            CurrentThemeId = pref.ThemeId;
                            var matched = _availableThemes.First(x => string.Equals(x.Id, pref.ThemeId, StringComparison.OrdinalIgnoreCase));
                            CurrentTheme = matched.ToMudTheme();
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        OnThemeChanged?.Invoke();
                    }
                }
            }
            catch (Exception)
            {
                // Prerender or JS not ready, ignore
            }

            IsInitialized = true;
        }
        catch (Exception)
        {
            IsInitialized = true;
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

    public async Task<bool> SelectThemeAsync(string themeId, IJSRuntime jsRuntime)
    {
        var matched = _availableThemes.FirstOrDefault(x => string.Equals(x.Id, themeId, StringComparison.OrdinalIgnoreCase));
        bool applied;
        if (matched != null)
        {
            CurrentThemeId = matched.Id;
            CurrentTheme = matched.ToMudTheme();
            applied = true;
        }
        else
        {
            CurrentThemeId = PresetThemes.DefaultThemeId;
            CurrentTheme = new MudTheme();
            applied = false;
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

        return applied;
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

