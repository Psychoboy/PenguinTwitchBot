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

        // Untrack session and update again
        service.UntrackActiveSession("session-1");
        await service.UpdateMetricsAsync();
    }
}

