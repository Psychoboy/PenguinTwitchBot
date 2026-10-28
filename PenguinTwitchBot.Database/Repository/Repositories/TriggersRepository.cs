using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class TriggersRepository : GenericRepository<TriggerType>, ITriggersRepository
    {
        public TriggersRepository(ApplicationDbContext context) : base(context)
        {
        }

        public new async Task<TriggerType?> GetByIdAsync(Guid id)
        {
            return await _context.Triggers
                .AsNoTracking()
                .Include(t => t.Action)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public new async Task<List<TriggerType>> GetAllAsync()
        {
            return await _context.Triggers
                .AsNoTracking()
                .Include(t => t.Action)
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        public async Task<List<TriggerType>> GetByTypeAsync(TriggerTypes type)
        {
            return await _context.Triggers
                .Include(t => t.Action)
                    .ThenInclude(a => a!.SubActions)
                .Where(t => t.Type == type)
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        public async Task<List<TriggerType>> GetTriggersForActionAsync(Guid actionId)
        {
            return await _context.Triggers
                .AsNoTracking()
                .Where(t => t.ActionId == actionId)
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

        public async Task DeleteAsync(Guid id)
        {
            var trigger = await _context.Triggers.FindAsync(id);
            if (trigger != null)
            {
                _context.Triggers.Remove(trigger);
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Efficiently queries triggers by TimerGroupId using the reference column instead of JSON deserialization.
        /// </summary>
        public async Task<List<TriggerType>> GetByTimerGroupIdAsync(Guid timerGroupId)
        {
            return await _context.Triggers
                .AsNoTracking()
                .Include(t => t.Action)
                    .ThenInclude(a => a!.SubActions)
                .Where(t => t.Type == TriggerTypes.Timer && t.TimerGroupId == timerGroupId)
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        /// <summary>
        /// Efficiently queries triggers by CommandId using the reference column instead of JSON deserialization.
        /// </summary>
        public async Task<List<TriggerType>> GetByCommandIdAsync(Guid commandId)
        {
            return await _context.Triggers
                .AsNoTracking()
                .Include(t => t.Action)
                    .ThenInclude(a => a!.SubActions)
                .Where(t => t.Type == TriggerTypes.Command && t.CommandId == commandId)
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        /// <summary>
        /// Efficiently queries triggers by DefaultCommandId using the reference column instead of JSON deserialization.
        /// </summary>
        public async Task<List<TriggerType>> GetByDefaultCommandIdAsync(Guid defaultCommandId)
        {
            return await _context.Triggers
                .AsNoTracking()
                .Include(t => t.Action)
                    .ThenInclude(a => a!.SubActions)
                .Where(t => t.Type == TriggerTypes.DefaultCommand && t.DefaultCommandId == defaultCommandId)
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        /// <summary>
        /// Efficiently queries triggers by KeywordId using the reference column instead of JSON deserialization.
        /// </summary>
        public async Task<List<TriggerType>> GetByKeywordIdAsync(Guid keywordId)
        {
            return await _context.Triggers
                .AsNoTracking()
                .Include(t => t.Action)
                    .ThenInclude(a => a!.SubActions)
                .Where(t => t.Type == TriggerTypes.Keyword && t.KeywordId == keywordId)
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        public async Task RenameByReferenceAsync(TriggerTypes type, Guid referenceId, string newName)
        {
            var now = DateTime.UtcNow;
            var query = type switch
            {
                TriggerTypes.Command => _context.Triggers.Where(t => t.Type == type && t.CommandId == referenceId),
                TriggerTypes.Keyword => _context.Triggers.Where(t => t.Type == type && t.KeywordId == referenceId),
                TriggerTypes.Timer => _context.Triggers.Where(t => t.Type == type && t.TimerGroupId == referenceId),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Only command, keyword and timer triggers are named after what they reference.")
            };

            await query
                .Where(t => t.Name != newName)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Name, newName)
                    .SetProperty(t => t.UpdatedAt, now));
        }
    }
}
