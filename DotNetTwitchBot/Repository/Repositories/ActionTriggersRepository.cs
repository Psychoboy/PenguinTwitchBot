using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Models.Actions.Triggers;
using Microsoft.EntityFrameworkCore;

namespace DotNetTwitchBot.Repository.Repositories
{
    // DEPRECATED: ActionTriggers junction table removed. Triggers now have direct one-to-many relationship with Actions.
    // This repository is kept for compatibility but should not be used.
    [Obsolete("ActionTrigger junction table has been removed. Use Triggers directly on Actions.")]
    public class ActionTriggersRepository : GenericRepository<ActionTrigger>, IActionTriggersRepository
    {
        public ActionTriggersRepository(ApplicationDbContext context) : base(context)
        {
        }

        public new async Task<ActionTrigger?> GetByIdAsync(int id)
        {
            throw new NotImplementedException("ActionTrigger junction table has been removed.");
        }

        public async Task<List<ActionTrigger>> GetByActionIdAsync(int actionId)
        {
            throw new NotImplementedException("ActionTrigger junction table has been removed.");
        }

        public async Task<List<ActionTrigger>> GetByTriggerIdAsync(int triggerId)
        {
            throw new NotImplementedException("ActionTrigger junction table has been removed.");
        }

        public new async Task<ActionTrigger> AddAsync(ActionTrigger actionTrigger)
        {
            throw new NotImplementedException("ActionTrigger junction table has been removed.");
        }

        public async Task DeleteAsync(int id)
        {
            throw new NotImplementedException("ActionTrigger junction table has been removed.");
        }

        public async Task DeleteByActionAndTriggerAsync(int actionId, int triggerId)
        {
            throw new NotImplementedException("ActionTrigger junction table has been removed.");
        }

        public async Task<bool> ExistsAsync(int actionId, int triggerId)
        {
            throw new NotImplementedException("ActionTrigger junction table has been removed.");
        }

        public override async Task BackupTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            // No-op: ActionTrigger junction table no longer exists
            logger?.LogDebug("Skipping ActionTrigger backup - entity deprecated");
            await Task.CompletedTask;
        }

        public override async Task RestoreTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            // No-op: ActionTrigger junction table no longer exists
            logger?.LogDebug("Skipping ActionTrigger restore - entity deprecated");
            await Task.CompletedTask;
        }
    }
}
