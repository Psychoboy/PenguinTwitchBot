using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Database.Bot.Models.Themes;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Services;

public sealed class UserThemePreferenceService(
    IServiceScopeFactory scopeFactory,
    IMemoryCache cache,
    ILogger<UserThemePreferenceService> logger) : IUserThemePreferenceService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
    private static string GetCacheKey(string userId) => $"user_theme_pref_{userId}";

    public async Task<UserThemePreference?> GetPreferenceAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;

        var cacheKey = GetCacheKey(userId);
        if (cache.TryGetValue(cacheKey, out UserThemePreference? cached) && cached != null)
        {
            return cached;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var pref = await unitOfWork.UserThemePreferences.GetByUserIdAsync(userId);
            if (pref != null)
            {
                cache.Set(cacheKey, pref, CacheDuration);
            }
            return pref;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting user theme preference for {UserId}", userId);
            return null;
        }
    }

    public async Task SavePreferenceAsync(string userId, string themeId, bool isDarkMode)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.UserThemePreferences.UpsertPreferenceAsync(userId, themeId, isDarkMode);

            var updatedPref = new UserThemePreference
            {
                UserId = userId,
                ThemeId = themeId,
                IsDarkMode = isDarkMode,
                UpdatedAt = DateTime.UtcNow
            };
            cache.Set(GetCacheKey(userId), updatedPref, CacheDuration);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving user theme preference for {UserId} (Theme: {ThemeId}, IsDarkMode: {IsDarkMode})",
                userId, themeId, isDarkMode);
        }
    }

    public async Task<List<ThemeUsageCount>> GetThemePreferenceCountsAsync()
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await unitOfWork.UserThemePreferences.GetPreferenceCountsAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting theme preference counts");
            return [];
        }
    }
}

