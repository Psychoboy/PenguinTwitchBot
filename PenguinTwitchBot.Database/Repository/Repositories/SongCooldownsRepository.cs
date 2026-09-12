using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Database.Bot.Models;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class SongCooldownsRepository(ApplicationDbContext context) : GenericRepository<SongCooldown>(context), ISongCooldownsRepository
    {
        public Task<SongCooldown?> GetBySongIdAsync(string songId)
        {
            var utcNow = DateTime.UtcNow;
            return _context.SongCooldowns
                .Where(x => x.SongId == songId && x.CooldownExpiresAt > utcNow)
                .OrderByDescending(x => x.CooldownExpiresAt)
                .FirstOrDefaultAsync();
        }

        public Task<List<SongCooldown>> GetActiveCooldownsAsync()
        {
            var utcNow = DateTime.UtcNow;
            return _context.SongCooldowns
                .AsNoTracking()
                .Where(x => x.CooldownExpiresAt > utcNow)
                .OrderBy(x => x.CooldownExpiresAt)
                .ToListAsync();
        }

        public async Task RemoveBySongIdAsync(string songId)
        {
            var items = await _context.SongCooldowns.Where(x => x.SongId == songId).ToListAsync();
            if (items.Count > 0)
            {
                _context.SongCooldowns.RemoveRange(items);
            }
        }

        public async Task ClearAllAsync()
        {
            var all = await _context.SongCooldowns.ToListAsync();
            if (all.Count > 0)
            {
                _context.SongCooldowns.RemoveRange(all);
            }
        }
    }
}
