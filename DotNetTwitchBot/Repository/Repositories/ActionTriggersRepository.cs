using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Models.Actions.Triggers;
using Microsoft.EntityFrameworkCore;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class ActionTriggersRepository : GenericRepository<ActionTrigger>, IActionTriggersRepository
    {
        public ActionTriggersRepository(ApplicationDbContext context) : base(context)
        {
        }

        public new async Task<ActionTrigger?> GetByIdAsync(int id)
        {
            return await _context.ActionTriggers
                .AsNoTracking()
                .Include(at => at.Action)
                .Include(at => at.Trigger)
                .FirstOrDefaultAsync(at => at.Id == id);
        }

        public async Task<List<ActionTrigger>> GetByActionIdAsync(int actionId)
        {
            return await _context.ActionTriggers
                .AsNoTracking()
                .Include(at => at.Trigger)
                .Where(at => at.ActionId == actionId)
                .OrderBy(at => at.Priority)
                .ToListAsync();
        }

        public async Task<List<ActionTrigger>> GetByTriggerIdAsync(int triggerId)
        {
            return await _context.ActionTriggers
                .AsNoTracking()
                .Include(at => at.Action)
                .Where(at => at.TriggerId == triggerId)
                .OrderBy(at => at.Priority)
                .ToListAsync();
        }

        public new async Task<ActionTrigger> AddAsync(ActionTrigger actionTrigger)
        {
            actionTrigger.CreatedAt = DateTime.UtcNow;
            await _context.ActionTriggers.AddAsync(actionTrigger);
            await _context.SaveChangesAsync();
            return actionTrigger;
        }

        public async Task DeleteAsync(int id)
        {
            var actionTrigger = await _context.ActionTriggers.FindAsync(id);
            if (actionTrigger != null)
            {
                _context.ActionTriggers.Remove(actionTrigger);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteByActionAndTriggerAsync(int actionId, int triggerId)
        {
            var actionTrigger = await _context.ActionTriggers
                .FirstOrDefaultAsync(at => at.ActionId == actionId && at.TriggerId == triggerId);
            
            if (actionTrigger != null)
            {
                _context.ActionTriggers.Remove(actionTrigger);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(int actionId, int triggerId)
        {
            return await _context.ActionTriggers
                .AsNoTracking()
                .AnyAsync(at => at.ActionId == actionId && at.TriggerId == triggerId);
        }
    }
}
