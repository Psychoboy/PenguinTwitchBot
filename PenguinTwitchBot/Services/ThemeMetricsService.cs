using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Models.Themes;

namespace PenguinTwitchBot.Services;

public sealed class ThemeMetricsService : IThemeMetricsService, IDisposable
{
    internal static readonly Prometheus.Gauge UserThemePreferences = Prometheus.Metrics.CreateGauge(
        "user_theme_preferences",
        "Number of users with configured theme preferences",
        ["theme_id", "theme_name", "mode"]);

    internal static readonly Prometheus.Gauge ActiveUsersByTheme = Prometheus.Metrics.CreateGauge(
        "active_users_by_theme",
        "Number of active connected dashboard users/circuits using each theme",
        ["theme_id", "theme_name", "mode"]);

    private readonly ConcurrentDictionary<string, (string ThemeId, bool IsDarkMode)> _activeSessions = new();
    private readonly IUserThemePreferenceService _preferenceService;
    private readonly ICustomThemeService _customThemeService;
    private readonly ILogger<ThemeMetricsService> _logger;
    private readonly HashSet<(string ThemeId, string ThemeName, string Mode)> _publishedLabels = [];
    private readonly object _metricsLock = new();

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
            Prometheus.Metrics.DefaultRegistry.AddBeforeCollectCallback(UpdateMetricsAsync);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register Prometheus before-collect callback for theme metrics");
        }

        _customThemeService.ThemesChanged += HandleThemesChanged;
    }

    private async void HandleThemesChanged(object? sender, EventArgs e)
    {
        try
        {
            await UpdateMetricsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating theme metrics after themes changed");
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

    public async Task UpdateMetricsAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return;
        try
        {
            var themes = _customThemeService.GetCachedThemes();

            // 1. Update Active Sessions Metric
            var activeCounts = _activeSessions.Values
                .GroupBy(s => (ThemeId: s.ThemeId ?? PresetThemes.DefaultThemeId, s.IsDarkMode))
                .ToDictionary(g => g.Key, g => g.Count());

            // 2. Update Persisted User Preferences Metric
            var prefCounts = (await _preferenceService.GetThemePreferenceCountsAsync())
                .ToDictionary(c => (c.ThemeId, c.IsDarkMode), c => c.Count);

            lock (_metricsLock)
            {
                var currentLabels = new HashSet<(string ThemeId, string ThemeName, string Mode)>();

                // Only record metrics for themes that actually exist
                foreach (var theme in themes)
                {
                    foreach (var isDark in new[] { true, false })
                    {
                        var modeStr = isDark ? "dark" : "light";
                        var labelTuple = (theme.Id, theme.Name, modeStr);
                        currentLabels.Add(labelTuple);

                        activeCounts.TryGetValue((theme.Id, isDark), out var activeCount);
                        ActiveUsersByTheme.WithLabels(theme.Id, theme.Name, modeStr).Set(activeCount);

                        prefCounts.TryGetValue((theme.Id, isDark), out var prefCount);
                        UserThemePreferences.WithLabels(theme.Id, theme.Name, modeStr).Set(prefCount);
                    }
                }

                // Remove stale label series (e.g. from renamed or removed themes)
                foreach (var stale in _publishedLabels)
                {
                    if (!currentLabels.Contains(stale))
                    {
                        try
                        {
                            ActiveUsersByTheme.RemoveLabelled(stale.ThemeId, stale.ThemeName, stale.Mode);
                        }
                        catch { }
                        try
                        {
                            UserThemePreferences.RemoveLabelled(stale.ThemeId, stale.ThemeName, stale.Mode);
                        }
                        catch { }
                    }
                }

                _publishedLabels.Clear();
                _publishedLabels.UnionWith(currentLabels);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating theme metrics");
        }
    }

    public void Dispose()
    {
        _customThemeService.ThemesChanged -= HandleThemesChanged;
    }
}
