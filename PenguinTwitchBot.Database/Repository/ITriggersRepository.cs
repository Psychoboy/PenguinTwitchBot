using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Database.Repository
{
    public interface ITriggersRepository
    {
        Task<TriggerType?> GetByIdAsync(Guid id);
        Task<List<TriggerType>> GetAllAsync();
        Task<List<TriggerType>> GetByTypeAsync(TriggerTypes type);
        Task<List<TriggerType>> GetTriggersForActionAsync(Guid actionId);
        Task<TriggerType> AddAsync(TriggerType trigger);
        Task<TriggerType> UpdateAsync(TriggerType trigger);
        Task DeleteAsync(Guid id);

        // New efficient query methods using reference columns
        Task<List<TriggerType>> GetByTimerGroupIdAsync(Guid timerGroupId);
        Task<List<TriggerType>> GetByCommandIdAsync(Guid commandId);
        Task<List<TriggerType>> GetByDefaultCommandIdAsync(Guid defaultCommandId);
        Task<List<TriggerType>> GetByKeywordIdAsync(Guid keywordId);
    }
}
