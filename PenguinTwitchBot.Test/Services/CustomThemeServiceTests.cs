using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Models.Themes;
using PenguinTwitchBot.Services;
using Xunit;

namespace PenguinTwitchBot.Test.Services;

public class CustomThemeServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _context;
    private readonly IServiceScopeFactory _scopeFactory;

    public CustomThemeServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(new ApplicationDbContext(options)));
        var serviceProvider = services.BuildServiceProvider();

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(_ => serviceProvider.CreateScope());
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetThemesAsync_ReturnsPresets_WhenNoneConfigured()
    {
        var service = new CustomThemeService(_scopeFactory);
        var themes = await service.GetThemesAsync();

        Assert.NotEmpty(themes);
        Assert.Contains(themes, t => t.Id == PresetThemes.DefaultThemeId);
        Assert.Contains(themes, t => t.Id == PresetThemes.ArcticThemeId);
        Assert.Contains(themes, t => t.IsDefault);
    }

    [Fact]
    public async Task SaveThemeAsync_AddsNewTheme_AndRaisesEvent()
    {
        var service = new CustomThemeService(_scopeFactory);
        bool eventFired = false;
        service.ThemesChanged += (s, e) => eventFired = true;

        var newTheme = new CustomThemeModel
        {
            Id = "streamer-custom-1",
            Name = "Streamer Special",
            Description = "Custom theme for stream",
            LightPalette = new ThemePaletteModel { Primary = "#FF0000" },
            DarkPalette = new ThemePaletteModel { Primary = "#990000" }
        };

        await service.SaveThemeAsync(newTheme);

        Assert.True(eventFired);

        var retrieved = await service.GetThemeByIdAsync("streamer-custom-1");
        Assert.NotNull(retrieved);
        Assert.Equal("Streamer Special", retrieved.Name);
        Assert.Equal("#ff0000", retrieved.LightPalette.Primary);
    }

    [Fact]
    public async Task SaveThemeAsync_WithIsDefault_ClearsOtherDefaults()
    {
        var service = new CustomThemeService(_scopeFactory);
        var initial = await service.GetThemesAsync();
        var defaultBefore = initial.First(x => x.IsDefault);

        var newTheme = new CustomThemeModel
        {
            Id = "new-default",
            Name = "New Default Theme",
            IsDefault = true,
            LightPalette = new ThemePaletteModel { Primary = "#00FF00" },
            DarkPalette = new ThemePaletteModel { Primary = "#009900" }
        };

        await service.SaveThemeAsync(newTheme);

        var themes = await service.GetThemesAsync();
        var retrievedNew = themes.First(x => x.Id == "new-default");
        var retrievedOld = themes.First(x => x.Id == defaultBefore.Id);

        Assert.True(retrievedNew.IsDefault);
        Assert.False(retrievedOld.IsDefault);
    }

    [Fact]
    public async Task SetDefaultThemeAsync_UpdatesDefaultCorrectly()
    {
        var service = new CustomThemeService(_scopeFactory);
        await service.GetThemesAsync(); // Ensure presets saved

        await service.SetDefaultThemeAsync(PresetThemes.ArcticThemeId);

        var themes = await service.GetThemesAsync();
        var arctic = themes.First(x => x.Id == PresetThemes.ArcticThemeId);
        var defaultTheme = themes.First(x => x.Id == PresetThemes.DefaultThemeId);

        Assert.True(arctic.IsDefault);
        Assert.False(defaultTheme.IsDefault);
    }

    [Fact]
    public async Task DeleteThemeAsync_RemovesCustomTheme()
    {
        var service = new CustomThemeService(_scopeFactory);
        var custom = new CustomThemeModel
        {
            Id = "theme-to-delete",
            Name = "Temporary",
            LightPalette = new ThemePaletteModel(),
            DarkPalette = new ThemePaletteModel()
        };

        await service.SaveThemeAsync(custom);
        var foundBefore = await service.GetThemeByIdAsync("theme-to-delete");
        Assert.NotNull(foundBefore);

        await service.DeleteThemeAsync("theme-to-delete");
        var foundAfter = await service.GetThemeByIdAsync("theme-to-delete");
        Assert.Null(foundAfter);
    }

    [Fact]
    public async Task DeleteThemeAsync_ThrowsWhenDeletingDefaultMudBlazor()
    {
        var service = new CustomThemeService(_scopeFactory);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteThemeAsync(PresetThemes.DefaultThemeId));
    }

    [Fact]
    public async Task DeleteThemeAsync_AllowsDeletingPresets_ExceptDefaultMudBlazor()
    {
        var service = new CustomThemeService(_scopeFactory);
        var initial = await service.GetThemesAsync();
        Assert.Contains(initial, t => t.Id == PresetThemes.CyberpunkThemeId);

        await service.DeleteThemeAsync(PresetThemes.CyberpunkThemeId);

        var themesAfter = await service.GetThemesAsync();
        Assert.DoesNotContain(themesAfter, t => t.Id == PresetThemes.CyberpunkThemeId);
    }

    [Fact]
    public async Task SetThemeEnabledAsync_DisablesTheme_AndRaisesEvent()
    {
        var service = new CustomThemeService(_scopeFactory);
        bool eventFired = false;
        service.ThemesChanged += (s, e) => eventFired = true;

        await service.GetThemesAsync(); // Ensure presets loaded
        await service.SetThemeEnabledAsync(PresetThemes.ArcticThemeId, false);

        Assert.True(eventFired);
        var arctic = await service.GetThemeByIdAsync(PresetThemes.ArcticThemeId);
        Assert.NotNull(arctic);
        Assert.False(arctic.IsEnabled);

        // Re-enable
        await service.SetThemeEnabledAsync(PresetThemes.ArcticThemeId, true);
        arctic = await service.GetThemeByIdAsync(PresetThemes.ArcticThemeId);
        Assert.NotNull(arctic);
        Assert.True(arctic.IsEnabled);
    }

    [Fact]
    public async Task SetThemeEnabledAsync_ThrowsWhenDisablingDefaultTheme()
    {
        var service = new CustomThemeService(_scopeFactory);
        var themes = await service.GetThemesAsync();
        var defaultTheme = themes.First(t => t.IsDefault);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetThemeEnabledAsync(defaultTheme.Id, false));
    }

    [Fact]
    public async Task SetDefaultThemeAsync_EnablesTheme_IfPreviouslyDisabled()
    {
        var service = new CustomThemeService(_scopeFactory);
        await service.GetThemesAsync();

        // Disable arctic first
        await service.SetThemeEnabledAsync(PresetThemes.ArcticThemeId, false);
        var arctic = await service.GetThemeByIdAsync(PresetThemes.ArcticThemeId);
        Assert.False(arctic!.IsEnabled);

        // Make arctic the default
        await service.SetDefaultThemeAsync(PresetThemes.ArcticThemeId);
        arctic = await service.GetThemeByIdAsync(PresetThemes.ArcticThemeId);
        Assert.True(arctic!.IsDefault);
        Assert.True(arctic.IsEnabled);
    }

    [Fact]
    public async Task ResetToDefaultsAsync_RestoresPresets()
    {
        var service = new CustomThemeService(_scopeFactory);
        var custom = new CustomThemeModel
        {
            Id = "temp-theme",
            Name = "Temporary Theme",
            LightPalette = new ThemePaletteModel(),
            DarkPalette = new ThemePaletteModel()
        };
        await service.SaveThemeAsync(custom);

        await service.ResetToDefaultsAsync();

        var themes = await service.GetThemesAsync();
        Assert.DoesNotContain(themes, t => t.Id == "temp-theme");
        Assert.Contains(themes, t => t.Id == PresetThemes.DefaultThemeId);
    }

    [Fact]
    public async Task SaveThemeAsync_ConcurrentSaves_DoNotCorruptOrThrow()
    {
        var service = new CustomThemeService(_scopeFactory);

        var tasks = Enumerable.Range(1, 5).Select(i =>
        {
            var theme = new CustomThemeModel
            {
                Id = $"concurrent-theme-{i}",
                Name = $"Theme {i}",
                LightPalette = new ThemePaletteModel { Primary = $"#00000{i}" },
                DarkPalette = new ThemePaletteModel { Primary = $"#00000{i}" }
            };
            return Task.Run(() => service.SaveThemeAsync(theme));
        });

        await Task.WhenAll(tasks);

        var themes = await service.GetThemesAsync();
        for (int i = 1; i <= 5; i++)
        {
            Assert.Contains(themes, t => t.Id == $"concurrent-theme-{i}");
        }
    }

    [Fact]
    public void ExportThemeToJson_ProducesValidJson()
    {
        var service = new CustomThemeService(_scopeFactory);
        var theme = new CustomThemeModel
        {
            Id = "theme-123",
            Name = "Export Test Theme",
            Description = "A test theme",
            LightPalette = new ThemePaletteModel { Primary = "#112233" },
            DarkPalette = new ThemePaletteModel { Primary = "#445566" }
        };

        var json = service.ExportThemeToJson(theme);

        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.Contains("Export Test Theme", json);
        Assert.Contains("#112233", json);
    }

    [Fact]
    public void ExportThemesToJson_ProducesValidPackageJson()
    {
        var service = new CustomThemeService(_scopeFactory);
        var list = new List<CustomThemeModel>
        {
            new() { Id = "t1", Name = "Theme One" },
            new() { Id = "t2", Name = "Theme Two" }
        };

        var json = service.ExportThemesToJson(list);

        Assert.Contains("themes", json);
        Assert.Contains("Theme One", json);
        Assert.Contains("Theme Two", json);
    }

    [Fact]
    public void ParseThemesFromJson_SingleTheme_ParsesSuccessfully()
    {
        var service = new CustomThemeService(_scopeFactory);
        var json = """
        {
            "id": "single-test",
            "name": "Single Parsed Theme",
            "description": "Testing parse",
            "lightPalette": { "primary": "#123456" },
            "darkPalette": { "primary": "#abcdef" }
        }
        """;

        var parsed = service.ParseThemesFromJson(json);

        Assert.Single(parsed);
        Assert.Equal("Single Parsed Theme", parsed[0].Name);
        Assert.Equal("#123456", parsed[0].LightPalette.Primary);
        Assert.Equal("#abcdef", parsed[0].DarkPalette.Primary);
    }

    [Fact]
    public void ParseThemesFromJson_Package_ParsesSuccessfully()
    {
        var service = new CustomThemeService(_scopeFactory);
        var json = """
        {
            "version": 1,
            "exportedAt": "2026-09-24T12:00:00Z",
            "themes": [
                { "id": "t1", "name": "Package Theme 1" },
                { "id": "t2", "name": "Package Theme 2" }
            ]
        }
        """;

        var parsed = service.ParseThemesFromJson(json);

        Assert.Equal(2, parsed.Count);
        Assert.Equal("Package Theme 1", parsed[0].Name);
        Assert.Equal("Package Theme 2", parsed[1].Name);
    }

    [Fact]
    public void ParseThemesFromJson_Array_ParsesSuccessfully()
    {
        var service = new CustomThemeService(_scopeFactory);
        var json = """
        [
            { "id": "arr1", "name": "Array Theme 1" },
            { "id": "arr2", "name": "Array Theme 2" }
        ]
        """;

        var parsed = service.ParseThemesFromJson(json);

        Assert.Equal(2, parsed.Count);
        Assert.Equal("Array Theme 1", parsed[0].Name);
        Assert.Equal("Array Theme 2", parsed[1].Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void ParseThemesFromJson_EmptyOrInvalid_ThrowsException(string invalidJson)
    {
        var service = new CustomThemeService(_scopeFactory);
        Assert.ThrowsAny<Exception>(() => service.ParseThemesFromJson(invalidJson));
    }

    [Fact]
    public async Task ImportThemesFromJsonAsync_SingleTheme_ImportsAndPersists()
    {
        var service = new CustomThemeService(_scopeFactory);
        bool eventFired = false;
        service.ThemesChanged += (s, e) => eventFired = true;

        var json = """
        {
            "id": "original-id",
            "name": "Imported Test Theme",
            "description": "Just imported",
            "lightPalette": { "primary": "#AABBCC" },
            "darkPalette": { "primary": "#112233" }
        }
        """;

        var imported = await service.ImportThemesFromJsonAsync(json, assignNewIds: true);

        Assert.True(eventFired);
        Assert.Single(imported);
        Assert.Equal("Imported Test Theme", imported[0].Name);
        Assert.NotEqual("original-id", imported[0].Id);
        Assert.False(imported[0].IsDefault);
        Assert.False(imported[0].IsBuiltIn);

        var allThemes = await service.GetThemesAsync();
        Assert.Contains(allThemes, t => t.Name == "Imported Test Theme");
    }

    [Fact]
    public async Task ImportThemesFromJsonAsync_DoesNotOverwritePresetIds()
    {
        var service = new CustomThemeService(_scopeFactory);
        // Attempt to import a theme with the preset ID "arctic"
        var json = $$"""
        {
            "id": "{{PresetThemes.ArcticThemeId}}",
            "name": "Malicious Overwrite Arctic",
            "lightPalette": { "primary": "#FF0000" },
            "darkPalette": { "primary": "#990000" }
        }
        """;

        var imported = await service.ImportThemesFromJsonAsync(json, assignNewIds: false);

        Assert.Single(imported);
        Assert.NotEqual(PresetThemes.ArcticThemeId, imported[0].Id);

        // Verify the original Arctic theme is untouched
        var allThemes = await service.GetThemesAsync();
        var originalArctic = allThemes.First(t => t.Id == PresetThemes.ArcticThemeId);
        Assert.Equal("Penguin Arctic", originalArctic.Name);
    }
}

