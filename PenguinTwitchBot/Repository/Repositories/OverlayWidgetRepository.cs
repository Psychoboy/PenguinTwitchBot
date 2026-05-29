using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Bot.Models.Overlay;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class OverlayWidgetRepository(ApplicationDbContext context)
        : GenericRepository<OverlayWidget>(context), IOverlayWidgetRepository
    {
        public async Task<List<OverlayWidget>> GetByLayoutIdAsync(int layoutId)
        {
            return await _context.OverlayWidgets
                .AsNoTracking()
                .Where(w => w.OverlayLayoutId == layoutId)
                .OrderBy(w => w.ZIndex)
                .ToListAsync();
        }
    }
}
