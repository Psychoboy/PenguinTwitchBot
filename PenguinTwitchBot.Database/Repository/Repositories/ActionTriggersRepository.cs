using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    // DEPRECATED: ActionTriggers junction table removed. Triggers now have direct one-to-many relationship with Actions.
    // This repository is kept for compatibility but should not be used.
    [Obsolete("ActionTrigger junction table has been removed. Use Triggers directly on Actions.")]
    public class ActionTriggersRepository : GenericRepository<ActionTrigger>, IActionTriggersRepository
    {
        public ActionTriggersRepository(ApplicationDbContext context) : base(context)
        {
        }

        public new Task<ActionTrigger?> GetByIdAsync(Guid id)
        {
            throw new NotImplementedException("ActionTrigger junction table has been removed.");
        }

        public Task<List<ActionTrigger>> GetByActionIdAsync(Guid actionId)
        {
            throw new NotImplementedException("ActionTrigger junction table has been removed.");
        }

        public Task<List<ActionTrigger>> GetByTriggerIdAsync(Guid triggerId)
        {
            throw new NotImplementedException("ActionTrigger junction table has been removed.");
        }

        public new Task<ActionTrigger> AddAsync(ActionTrigger actionTrigger)
        {
            throw new NotImplementedException("ActionTrigger junction table has been removed.");
        }

        public Task DeleteAsync(Guid id)
        {
            throw new NotImplementedException("ActionTrigger junction table has been removed.");
        }

        public Task DeleteByActionAndTriggerAsync(Guid actionId, Guid triggerId)
        {
            throw new NotImplementedException("ActionTrigger junction table has been removed.");
        }

        public Task<bool> ExistsAsync(Guid actionId, Guid triggerId)
        {
            throw new NotImplementedException("ActionTrigger junction table has been removed.");
        }
    }
}
