using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Models.Themes;

namespace PenguinTwitchBot.Services;

public sealed class CustomThemeService(IServiceScopeFactory scopeFactory) : ICustomThemeService, IDisposable
{
    private const string SettingName = "CustomThemes";
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly object _cacheLock = new();
    private List<CustomThemeModel>? _cachedThemes;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public event EventHandler? ThemesChanged;

    public IReadOnlyList<CustomThemeModel> GetCachedThemes()
    {
        lock (_cacheLock)
        {
            if (_cachedThemes != null)
            {
                return _cachedThemes;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var setting = unitOfWork.Settings.Get(x => x.Name == SettingName).FirstOrDefault();

                if (setting == null || string.IsNullOrWhiteSpace(setting.StringSetting))
                {
                    _cachedThemes = PresetThemes.GetPresets();
                    return _cachedThemes;
                }

                var list = JsonSerializer.Deserialize<List<CustomThemeModel>>(setting.StringSetting, JsonOptions);
                if (list == null || list.Count == 0)
                {
                    _cachedThemes = PresetThemes.GetPresets();
                    return _cachedThemes;
                }

                if (!list.Any(x => x.IsDefault))
                {
                    var defaultPreset = list.FirstOrDefault(x => x.Id == PresetThemes.DefaultThemeId) ?? list.First();
                    defaultPreset.IsDefault = true;
                    defaultPreset.IsEnabled = true;
                }

                foreach (var t in list)
                {
                    t.LightPalette.NormalizeColors();
                    t.DarkPalette.NormalizeColors();
                }

                _cachedThemes = list;
                return _cachedThemes;
            }
            catch
            {
                _cachedThemes = PresetThemes.GetPresets();
                return _cachedThemes;
            }
        }
    }

    public CustomThemeModel GetDefaultTheme()
    {
        var themes = GetCachedThemes();
        return themes.FirstOrDefault(x => x.IsDefault)
            ?? themes.FirstOrDefault(x => x.Id == PresetThemes.DefaultThemeId)
            ?? themes.First();
    }

    public async Task<List<CustomThemeModel>> GetThemesAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingName)).FirstOrDefault();

        if (setting == null || string.IsNullOrWhiteSpace(setting.StringSetting))
        {
            var presets = PresetThemes.GetPresets();
            await SaveThemesInternalAsync(unitOfWork, presets);
            lock (_cacheLock) { _cachedThemes = presets; }
            return presets;
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<CustomThemeModel>>(setting.StringSetting, JsonOptions);
            if (list == null || list.Count == 0)
            {
                var presets = PresetThemes.GetPresets();
                await SaveThemesInternalAsync(unitOfWork, presets);
                lock (_cacheLock) { _cachedThemes = presets; }
                return presets;
            }

            // Ensure at least one theme is marked default
            if (!list.Any(x => x.IsDefault))
            {
                var defaultPreset = list.FirstOrDefault(x => x.Id == PresetThemes.DefaultThemeId) ?? list.First();
                defaultPreset.IsDefault = true;
                defaultPreset.IsEnabled = true;
            }

            // Normalize colors for every loaded theme
            foreach (var t in list)
            {
                t.LightPalette.NormalizeColors();
                t.DarkPalette.NormalizeColors();
            }

            lock (_cacheLock) { _cachedThemes = list; }
            return list;
        }
        catch
        {
            var presets = PresetThemes.GetPresets();
            lock (_cacheLock) { _cachedThemes = presets; }
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
        await _lock.WaitAsync();
        try
        {
            using var scope = scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var themes = await GetThemesAsync();

            theme.LightPalette.NormalizeColors();
            theme.DarkPalette.NormalizeColors();

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
            lock (_cacheLock) { _cachedThemes = themes; }
        }
        finally
        {
            _lock.Release();
        }

        ThemesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteThemeAsync(string id)
    {
        if (string.Equals(id, PresetThemes.DefaultThemeId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot delete the default theme.");
        }

        await _lock.WaitAsync();
        try
        {
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

            // Clean up any user preferences referencing the deleted theme
            var orphanPrefs = await unitOfWork.UserThemePreferences.GetAsync(x => x.ThemeId == id);
            unitOfWork.UserThemePreferences.RemoveRange(orphanPrefs);


            await SaveThemesInternalAsync(unitOfWork, themes);
            lock (_cacheLock) { _cachedThemes = themes; }
        }
        finally
        {
            _lock.Release();
        }

        ThemesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetDefaultThemeAsync(string id)
    {
        await _lock.WaitAsync();
        try
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
            lock (_cacheLock) { _cachedThemes = themes; }
        }
        finally
        {
            _lock.Release();
        }

        ThemesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetThemeEnabledAsync(string id, bool isEnabled)
    {
        await _lock.WaitAsync();
        try
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
            lock (_cacheLock) { _cachedThemes = themes; }
        }
        finally
        {
            _lock.Release();
        }

        ThemesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task ResetToDefaultsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            using var scope = scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var presets = PresetThemes.GetPresets();

            var presetIds = new HashSet<string>(presets.Select(p => p.Id), StringComparer.OrdinalIgnoreCase);
            var orphanPrefs = await unitOfWork.UserThemePreferences.GetAsync(x => !presetIds.Contains(x.ThemeId));
            unitOfWork.UserThemePreferences.RemoveRange(orphanPrefs);

            await SaveThemesInternalAsync(unitOfWork, presets);
            lock (_cacheLock) { _cachedThemes = presets; }
        }
        finally
        {
            _lock.Release();
        }

        ThemesChanged?.Invoke(this, EventArgs.Empty);
    }

    public string ExportThemeToJson(CustomThemeModel theme)
    {
        return JsonSerializer.Serialize(theme, JsonOptions);
    }

    public string ExportThemesToJson(IEnumerable<CustomThemeModel> themes)
    {
        var package = new ThemeExportPackage
        {
            Version = 1,
            ExportedAt = DateTime.UtcNow,
            Themes = themes.ToList()
        };
        return JsonSerializer.Serialize(package, JsonOptions);
    }

    public List<CustomThemeModel> ParseThemesFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Theme JSON cannot be empty.", nameof(json));
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        List<CustomThemeModel> result = [];

        if (root.ValueKind == JsonValueKind.Array)
        {
            var list = JsonSerializer.Deserialize<List<CustomThemeModel>>(json, JsonOptions);
            if (list != null)
            {
                result.AddRange(list);
            }
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            if ((root.TryGetProperty("themes", out var themesProp) || root.TryGetProperty("Themes", out themesProp)) && themesProp.ValueKind == JsonValueKind.Array)
            {
                var package = JsonSerializer.Deserialize<ThemeExportPackage>(json, JsonOptions);
                if (package?.Themes != null)
                {
                    result.AddRange(package.Themes);
                }
            }
            else
            {
                // Verify the object contains theme properties and is not empty or arbitrary JSON
                var hasThemeProperty = root.EnumerateObject().Any(prop =>
                    prop.NameEquals("name") ||
                    prop.NameEquals("Name") ||
                    prop.NameEquals("lightPalette") ||
                    prop.NameEquals("LightPalette") ||
                    prop.NameEquals("darkPalette") ||
                    prop.NameEquals("DarkPalette") ||
                    prop.NameEquals("description") ||
                    prop.NameEquals("Description"));

                if (!hasThemeProperty)
                {
                    throw new JsonException("JSON object does not contain theme definition properties.");
                }

                var single = JsonSerializer.Deserialize<CustomThemeModel>(json, JsonOptions);
                if (single != null)
                {
                    result.Add(single);
                }
            }
        }
        else
        {
            throw new JsonException("Invalid theme JSON structure. Expected an object or array.");
        }

        if (result.Count == 0)
        {
            throw new JsonException("No valid themes found in the provided JSON.");
        }

        foreach (var theme in result)
        {
            theme.LightPalette ??= new ThemePaletteModel();
            theme.DarkPalette ??= new ThemePaletteModel();

            theme.LightPalette.NormalizeColors();
            theme.DarkPalette.NormalizeColors();

            if (string.IsNullOrWhiteSpace(theme.Name))
            {
                theme.Name = "Imported Theme";
            }
        }

        return result;
    }

    public async Task<List<CustomThemeModel>> ImportThemesFromJsonAsync(string json, bool assignNewIds = true)
    {
        var parsedThemes = ParseThemesFromJson(json);

        await _lock.WaitAsync();
        try
        {
            using var scope = scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var existingThemes = await GetThemesAsync();

            var presetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                PresetThemes.DefaultThemeId,
                PresetThemes.ArcticThemeId,
                PresetThemes.MidnightPurpleThemeId,
                PresetThemes.CyberpunkThemeId,
                PresetThemes.ForestEmeraldThemeId
            };

            var seenImportedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var imported in parsedThemes)
            {
                imported.IsBuiltIn = false;
                imported.IsDefault = false;

                if (assignNewIds || string.IsNullOrWhiteSpace(imported.Id) || presetIds.Contains(imported.Id) || seenImportedIds.Contains(imported.Id))
                {
                    imported.Id = Guid.NewGuid().ToString();
                }

                seenImportedIds.Add(imported.Id);

                var existingIdx = existingThemes.FindIndex(x => string.Equals(x.Id, imported.Id, StringComparison.OrdinalIgnoreCase));
                if (existingIdx >= 0)
                {
                    imported.IsDefault = existingThemes[existingIdx].IsDefault;
                    if (imported.IsDefault)
                    {
                        imported.IsEnabled = true;
                    }
                    existingThemes[existingIdx] = imported;
                }
                else
                {
                    existingThemes.Add(imported);
                }
            }

            await SaveThemesInternalAsync(unitOfWork, existingThemes);
            lock (_cacheLock) { _cachedThemes = existingThemes; }
        }
        finally
        {
            _lock.Release();
        }

        ThemesChanged?.Invoke(this, EventArgs.Empty);
        return parsedThemes;
    }

    public void Dispose()
    {
        _lock.Dispose();
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

