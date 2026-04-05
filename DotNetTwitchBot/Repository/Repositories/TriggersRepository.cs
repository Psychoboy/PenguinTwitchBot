using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Models.Actions.Triggers;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

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
                .Include(t => t.Action)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<TriggerType?> GetByNameAsync(string name)
        {
            return await _context.Triggers
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Name == name);
        }

        public new async Task<List<TriggerType>> GetAllAsync()
        {
            return await _context.Triggers
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        public async Task<List<TriggerType>> GetByTypeAsync(TriggerTypes type)
        {
            return await _context.Triggers
                .AsNoTracking()
                .Include(t => t.Action)
                    .ThenInclude(a => a!.SubActions)
                .Where(t => t.Type == type)
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        public async Task<List<TriggerType>> GetTriggersForActionAsync(int actionId)
        {
            return await _context.Triggers
                .AsNoTracking()
                .Where(t => t.ActionId == actionId)
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        public async Task<List<TriggerType>> GetTriggersByCommandIdAsync(int commandId)
        {
            return await _context.Triggers
                .AsNoTracking()
                .Where(t => t.Type == TriggerTypes.Command && t.Configuration.Contains($"\"CommandId\":{commandId}"))
                .ToListAsync();
        }

        public async Task<List<TriggerType>> GetTriggersByTimerGroupIdAsync(int timerId)
        {
            return await _context.Triggers
                .AsNoTracking()
                .Include(t => t.Action)
                    .ThenInclude(a => a!.SubActions)
                .Where(t => t.Type == TriggerTypes.Timer && t.Configuration.Contains($"\"TimerGroupId\":{timerId}"))
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

        public override async Task BackupTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            // No-op: Triggers are now backed up as children of Actions
            // They are included when ActionsRepository backs up with .Include(a => a.Triggers)
            logger?.LogDebug("Skipping TriggerType backup - backed up with Actions");
            await Task.CompletedTask;
        }

        public override async Task RestoreTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            // No-op: Triggers are now restored as children of Actions
            // They are restored when ActionsRepository restores with .Include(a => a.Triggers)
            logger?.LogDebug("Skipping TriggerType restore - restored with Actions");
            await Task.CompletedTask;
        }
    }
}
