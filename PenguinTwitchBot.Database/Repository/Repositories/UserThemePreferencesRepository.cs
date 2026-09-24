using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Themes;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class UserThemePreferencesRepository(ApplicationDbContext context)
        : GenericRepository<UserThemePreference>(context), IUserThemePreferencesRepository
    {
        public async Task<UserThemePreference?> GetByUserIdAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return null;
            return await _context.UserThemePreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId);
        }

        public async Task UpsertPreferenceAsync(string userId, string themeId, bool isDarkMode)
        {
            if (string.IsNullOrWhiteSpace(userId)) return;
            var existing = await _context.UserThemePreferences.FirstOrDefaultAsync(x => x.UserId == userId);
            if (existing != null)
            {
                existing.ThemeId = themeId;
                existing.IsDarkMode = isDarkMode;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                await _context.UserThemePreferences.AddAsync(new UserThemePreference
                {
                    UserId = userId,
                    ThemeId = themeId,
                    IsDarkMode = isDarkMode,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync();
        }

        public async Task<List<ThemeUsageCount>> GetPreferenceCountsAsync()
        {
            return await _context.UserThemePreferences
                .AsNoTracking()
                .GroupBy(x => new { x.ThemeId, x.IsDarkMode })
                .Select(g => new ThemeUsageCount(g.Key.ThemeId, g.Key.IsDarkMode, g.Count()))
                .ToListAsync();
        }
    }
}

