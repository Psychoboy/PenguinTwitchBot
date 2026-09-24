using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Database.Bot.Models.Themes;
using PenguinTwitchBot.Models.Themes;
using PenguinTwitchBot.Services;
using Xunit;

namespace PenguinTwitchBot.Test.Services;

public class ThemeMetricsServiceTests
{
    private readonly IUserThemePreferenceService _preferenceService;
    private readonly ICustomThemeService _customThemeService;
    private readonly ILogger<ThemeMetricsService> _logger;
    private readonly List<CustomThemeModel> _themes;

    public ThemeMetricsServiceTests()
    {
        _preferenceService = Substitute.For<IUserThemePreferenceService>();
        _customThemeService = Substitute.For<ICustomThemeService>();
        _logger = Substitute.For<ILogger<ThemeMetricsService>>();

        _themes = PresetThemes.GetPresets();
        _customThemeService.GetCachedThemes().Returns(_themes);
    }

    [Fact]
    public async Task UpdateMetricsAsync_RunsWithoutError_AndSetsMetrics()
    {
        _preferenceService.GetThemePreferenceCountsAsync().Returns(Task.FromResult(new List<ThemeUsageCount>
        {
            new(PresetThemes.DefaultThemeId, true, 3),
            new(PresetThemes.ArcticThemeId, false, 1)
        }));

        var service = new ThemeMetricsService(_preferenceService, _customThemeService, _logger);
        service.TrackActiveSession("session-1", PresetThemes.DefaultThemeId, true);
        service.TrackActiveSession("session-2", PresetThemes.ArcticThemeId, false);

        await service.UpdateMetricsAsync();

        var defaultTheme = _themes.First(t => t.Id == PresetThemes.DefaultThemeId);
        var arcticTheme = _themes.First(t => t.Id == PresetThemes.ArcticThemeId);

        // Verify emitted values for user_theme_preferences
        Assert.Equal(3, ThemeMetricsService.UserThemePreferences.WithLabels(PresetThemes.DefaultThemeId, defaultTheme.Name, "dark").Value);
        Assert.Equal(0, ThemeMetricsService.UserThemePreferences.WithLabels(PresetThemes.DefaultThemeId, defaultTheme.Name, "light").Value);
        Assert.Equal(1, ThemeMetricsService.UserThemePreferences.WithLabels(PresetThemes.ArcticThemeId, arcticTheme.Name, "light").Value);
        Assert.Equal(0, ThemeMetricsService.UserThemePreferences.WithLabels(PresetThemes.ArcticThemeId, arcticTheme.Name, "dark").Value);

        // Verify emitted values for active_users_by_theme
        Assert.Equal(1, ThemeMetricsService.ActiveUsersByTheme.WithLabels(PresetThemes.DefaultThemeId, defaultTheme.Name, "dark").Value);
        Assert.Equal(1, ThemeMetricsService.ActiveUsersByTheme.WithLabels(PresetThemes.ArcticThemeId, arcticTheme.Name, "light").Value);

        // Untrack session and update again
        service.UntrackActiveSession("session-1");
        await service.UpdateMetricsAsync();

        // Verify active-session gauge reflects reduced count
        Assert.Equal(0, ThemeMetricsService.ActiveUsersByTheme.WithLabels(PresetThemes.DefaultThemeId, defaultTheme.Name, "dark").Value);
        Assert.Equal(1, ThemeMetricsService.ActiveUsersByTheme.WithLabels(PresetThemes.ArcticThemeId, arcticTheme.Name, "light").Value);

        // Verify preference counts are preserved
        Assert.Equal(3, ThemeMetricsService.UserThemePreferences.WithLabels(PresetThemes.DefaultThemeId, defaultTheme.Name, "dark").Value);
        Assert.Equal(1, ThemeMetricsService.UserThemePreferences.WithLabels(PresetThemes.ArcticThemeId, arcticTheme.Name, "light").Value);
    }

    [Fact]
    public async Task UpdateMetricsAsync_WhenThemeRenamed_RemovesOldLabelAndSetsNewLabel()
    {
        var customTheme = new CustomThemeModel
        {
            Id = "test-rename-id",
            Name = "Original Name",
            LightPalette = new ThemePaletteModel(),
            DarkPalette = new ThemePaletteModel()
        };
        _themes.Add(customTheme);

        _preferenceService.GetThemePreferenceCountsAsync().Returns(Task.FromResult(new List<ThemeUsageCount>
        {
            new("test-rename-id", true, 1)
        }));

        var service = new ThemeMetricsService(_preferenceService, _customThemeService, _logger);
        await service.UpdateMetricsAsync();

        Assert.Equal(1, ThemeMetricsService.UserThemePreferences.WithLabels("test-rename-id", "Original Name", "dark").Value);

        // Rename theme
        customTheme.Name = "New Name";
        await service.UpdateMetricsAsync();

        // New name label has the count
        Assert.Equal(1, ThemeMetricsService.UserThemePreferences.WithLabels("test-rename-id", "New Name", "dark").Value);

        // Old name label was removed (re-querying returns default 0, not old value 1)
        Assert.Equal(0, ThemeMetricsService.UserThemePreferences.WithLabels("test-rename-id", "Original Name", "dark").Value);

        // Clean up
        _themes.Remove(customTheme);
        await service.UpdateMetricsAsync();
    }

    [Fact]
    public async Task UpdateMetricsAsync_WhenThemeRemoved_RemovesDeletedThemeMetrics()
    {
        var tempTheme = new CustomThemeModel
        {
            Id = "test-delete-id",
            Name = "Temp Delete Theme",
            LightPalette = new ThemePaletteModel(),
            DarkPalette = new ThemePaletteModel()
        };
        _themes.Add(tempTheme);

        _preferenceService.GetThemePreferenceCountsAsync().Returns(Task.FromResult(new List<ThemeUsageCount>
        {
            new("test-delete-id", true, 1)
        }));

        var service = new ThemeMetricsService(_preferenceService, _customThemeService, _logger);
        service.TrackActiveSession("session-temp", "test-delete-id", true);

        await service.UpdateMetricsAsync();

        Assert.Equal(1, ThemeMetricsService.UserThemePreferences.WithLabels("test-delete-id", "Temp Delete Theme", "dark").Value);
        Assert.Equal(1, ThemeMetricsService.ActiveUsersByTheme.WithLabels("test-delete-id", "Temp Delete Theme", "dark").Value);

        // Remove theme
        _themes.Remove(tempTheme);
        service.UntrackActiveSession("session-temp");
        _preferenceService.GetThemePreferenceCountsAsync().Returns(Task.FromResult(new List<ThemeUsageCount>()));

        await service.UpdateMetricsAsync();

        // Stale labels were removed from Prometheus
        Assert.Equal(0, ThemeMetricsService.UserThemePreferences.WithLabels("test-delete-id", "Temp Delete Theme", "dark").Value);
        Assert.Equal(0, ThemeMetricsService.ActiveUsersByTheme.WithLabels("test-delete-id", "Temp Delete Theme", "dark").Value);
    }
}

