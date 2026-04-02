using DotNetTwitchBot.Bot.Models.Actions.Triggers;

namespace DotNetTwitchBot.Repository
{
    public interface ITriggersRepository
    {
        Task<TriggerType?> GetByIdAsync(int id);
        Task<TriggerType?> GetByNameAsync(string name);
        Task<List<TriggerType>> GetAllAsync();
        Task<List<TriggerType>> GetByTypeAsync(TriggerTypes type);
        Task<List<TriggerType>> GetTriggersForActionAsync(int actionId);
        Task<TriggerType> AddAsync(TriggerType trigger);
        Task<TriggerType> UpdateAsync(TriggerType trigger);
        Task DeleteAsync(int id);
        Task<bool> ExistsAsync(string name);
    }
}
