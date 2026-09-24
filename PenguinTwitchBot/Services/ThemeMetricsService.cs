using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Models.Themes;

namespace PenguinTwitchBot.Services;

public sealed class ThemeMetricsService : IThemeMetricsService
{
    private static readonly Prometheus.Gauge UserThemePreferences = Prometheus.Metrics.CreateGauge(
        "user_theme_preferences",
        "Number of users with configured theme preferences",
        ["theme_id", "theme_name", "mode"]);

    private static readonly Prometheus.Gauge ActiveUsersByTheme = Prometheus.Metrics.CreateGauge(
        "active_users_by_theme",
        "Number of active connected dashboard users/circuits using each theme",
        ["theme_id", "theme_name", "mode"]);

    private readonly ConcurrentDictionary<string, (string ThemeId, bool IsDarkMode)> _activeSessions = new();
    private readonly IUserThemePreferenceService _preferenceService;
    private readonly ICustomThemeService _customThemeService;
    private readonly ILogger<ThemeMetricsService> _logger;

    public ThemeMetricsService(
        IUserThemePreferenceService preferenceService,
        ICustomThemeService customThemeService,
        ILogger<ThemeMetricsService> logger)
    {
        _preferenceService = preferenceService;
        _customThemeService = customThemeService;
        _logger = logger;

        try
        {
            Prometheus.Metrics.DefaultRegistry.AddBeforeCollectCallback(async () =>
            {
                await UpdateMetricsAsync();
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register Prometheus before-collect callback for theme metrics");
        }
    }

    public void TrackActiveSession(string sessionId, string themeId, bool isDarkMode)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;
        _activeSessions[sessionId] = (themeId, isDarkMode);
    }

    public void UntrackActiveSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;
        _activeSessions.TryRemove(sessionId, out _);
    }

    public async Task UpdateMetricsAsync()
    {
        try
        {
            var themes = _customThemeService.GetCachedThemes();
            var themeLookup = themes.ToDictionary(t => t.Id, t => t.Name, StringComparer.OrdinalIgnoreCase);

            // 1. Update Active Sessions Metric
            var activeCounts = _activeSessions.Values
                .GroupBy(s => (ThemeId: s.ThemeId ?? PresetThemes.DefaultThemeId, s.IsDarkMode))
                .ToDictionary(g => g.Key, g => g.Count());

            // 2. Update Persisted User Preferences Metric
            var prefCounts = (await _preferenceService.GetThemePreferenceCountsAsync())
                .ToDictionary(c => (c.ThemeId, c.IsDarkMode), c => c.Count);

            var allKeys = new HashSet<(string ThemeId, bool IsDarkMode)>();

            // Include all available themes for both dark and light modes
            foreach (var theme in themes)
            {
                allKeys.Add((theme.Id, true));
                allKeys.Add((theme.Id, false));
            }

            // Include any additional themes from active sessions or saved preferences
            foreach (var key in activeCounts.Keys) allKeys.Add(key);
            foreach (var key in prefCounts.Keys) allKeys.Add(key);

            foreach (var (themeId, isDark) in allKeys)
            {
                var modeStr = isDark ? "dark" : "light";
                var themeName = themeLookup.TryGetValue(themeId, out var name) ? name : themeId;

                activeCounts.TryGetValue((themeId, isDark), out var activeCount);
                ActiveUsersByTheme.WithLabels(themeId, themeName, modeStr).Set(activeCount);

                prefCounts.TryGetValue((themeId, isDark), out var prefCount);
                UserThemePreferences.WithLabels(themeId, themeName, modeStr).Set(prefCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating theme metrics");
        }
    }
}

