using DotNetTwitchBot.Bot.Models.Actions.Triggers;

namespace DotNetTwitchBot.Repository
{
    public interface ITriggersRepository
    {
        Task<TriggerType?> GetByIdAsync(int id);
        Task<List<TriggerType>> GetAllAsync();
        Task<List<TriggerType>> GetByTypeAsync(TriggerTypes type);
        Task<List<TriggerType>> GetTriggersForActionAsync(int actionId);
        Task<TriggerType> AddAsync(TriggerType trigger);
        Task<TriggerType> UpdateAsync(TriggerType trigger);
        Task DeleteAsync(int id);

        // New efficient query methods using reference columns
        Task<List<TriggerType>> GetByTimerGroupIdAsync(int timerGroupId);
        Task<List<TriggerType>> GetByCommandIdAsync(int commandId);
        Task<List<TriggerType>> GetByDefaultCommandIdAsync(int defaultCommandId);
    }
}
