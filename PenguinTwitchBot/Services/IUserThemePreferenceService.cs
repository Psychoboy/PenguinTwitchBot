using System.Collections.Generic;
using System.Threading.Tasks;
using PenguinTwitchBot.Database.Bot.Models.Themes;

namespace PenguinTwitchBot.Services;

public interface IUserThemePreferenceService
{
    Task<UserThemePreference?> GetPreferenceAsync(string userId);
    Task SavePreferenceAsync(string userId, string themeId, bool isDarkMode);
    Task<List<ThemeUsageCount>> GetThemePreferenceCountsAsync();
}

