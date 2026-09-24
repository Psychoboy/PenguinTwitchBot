using System.Collections.Generic;
using System.Threading.Tasks;
using PenguinTwitchBot.Database.Bot.Models.Themes;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IUserThemePreferencesRepository : IGenericRepository<UserThemePreference>
    {
        Task<UserThemePreference?> GetByUserIdAsync(string userId);
        Task UpsertPreferenceAsync(string userId, string themeId, bool isDarkMode);
        Task<List<ThemeUsageCount>> GetPreferenceCountsAsync();
    }
}

