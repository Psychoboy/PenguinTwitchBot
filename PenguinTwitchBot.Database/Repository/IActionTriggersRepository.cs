using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IActionTriggersRepository
    {
        Task<ActionTrigger?> GetByIdAsync(Guid id);
        Task<List<ActionTrigger>> GetByActionIdAsync(Guid actionId);
        Task<List<ActionTrigger>> GetByTriggerIdAsync(Guid triggerId);
        Task<ActionTrigger> AddAsync(ActionTrigger actionTrigger);
        Task DeleteAsync(Guid id);
        Task DeleteByActionAndTriggerAsync(Guid actionId, Guid triggerId);
        Task<bool> ExistsAsync(Guid actionId, Guid triggerId);
    }
}
