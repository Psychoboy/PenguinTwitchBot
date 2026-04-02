using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Models.Actions.Triggers;
using Microsoft.EntityFrameworkCore;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class TriggersRepository : GenericRepository<TriggerType>, ITriggersRepository
    {
        public TriggersRepository(ApplicationDbContext context) : base(context)
        {
        }

        public new async Task<TriggerType?> GetByIdAsync(int id)
        {
            return await _context.Triggers
                .AsNoTracking()
                .Include(t => t.ActionTriggers)
                .ThenInclude(at => at.Action)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<TriggerType?> GetByNameAsync(string name)
        {
            return await _context.Triggers
                .AsNoTracking()
                .Include(t => t.ActionTriggers)
                .FirstOrDefaultAsync(t => t.Name == name);
        }

        public new async Task<List<TriggerType>> GetAllAsync()
        {
            return await _context.Triggers
                .AsNoTracking()
                .Include(t => t.ActionTriggers)
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        public async Task<List<TriggerType>> GetByTypeAsync(TriggerTypes type)
        {
            return await _context.Triggers
                .AsNoTracking()
                .Include(t => t.ActionTriggers)
                .Where(t => t.Type == type)
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        public async Task<List<TriggerType>> GetTriggersForActionAsync(int actionId)
        {
            return await _context.Triggers
                .AsNoTracking()
                .Include(t => t.ActionTriggers)
                .Where(t => t.ActionTriggers.Any(at => at.ActionId == actionId))
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        public new async Task<TriggerType> AddAsync(TriggerType trigger)
        {
            trigger.CreatedAt = DateTime.UtcNow;
            trigger.UpdatedAt = DateTime.UtcNow;
            await _context.Triggers.AddAsync(trigger);
            await _context.SaveChangesAsync();
            return trigger;
        }

        public async Task<TriggerType> UpdateAsync(TriggerType trigger)
        {
            trigger.UpdatedAt = DateTime.UtcNow;
            _context.Triggers.Update(trigger);
            await _context.SaveChangesAsync();
            return trigger;
        }

        public async Task DeleteAsync(int id)
        {
            var trigger = await _context.Triggers.FindAsync(id);
            if (trigger != null)
            {
                _context.Triggers.Remove(trigger);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(string name)
        {
            return await _context.Triggers.AsNoTracking().AnyAsync(t => t.Name == name);
        }
    }
}
