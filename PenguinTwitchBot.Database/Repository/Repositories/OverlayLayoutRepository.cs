using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Database.Bot.Models.Overlay;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class OverlayLayoutRepository(ApplicationDbContext context)
        : GenericRepository<OverlayLayout>(context), IOverlayLayoutRepository
    {
        public async Task<OverlayLayout?> GetByNameAsync(string name)
        {
            return await _context.OverlayLayouts
                .AsNoTracking()
                .Include(l => l.Widgets)
                .FirstOrDefaultAsync(l => l.Name == name);
        }

        public async Task<OverlayLayout?> GetDefaultAsync()
        {
            return await _context.OverlayLayouts
                .AsNoTracking()
                .Include(l => l.Widgets)
                .FirstOrDefaultAsync(l => l.IsDefault);
        }

        public async Task<List<OverlayLayout>> GetAllWithWidgetsAsync()
        {
            return await _context.OverlayLayouts
                .AsNoTracking()
                .Include(l => l.Widgets)
                .OrderBy(l => l.Name)
                .ToListAsync();
        }

        public async Task<OverlayLayout?> GetByIdWithWidgetsAsync(int id)
        {
            return await _context.OverlayLayouts
                .AsNoTracking()
                .Include(l => l.Widgets)
                .FirstOrDefaultAsync(l => l.Id == id);
        }
    }
}
