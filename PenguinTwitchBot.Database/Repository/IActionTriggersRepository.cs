using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IActionTriggersRepository
    {
        Task<ActionTrigger?> GetByIdAsync(int id);
        Task<List<ActionTrigger>> GetByActionIdAsync(int actionId);
        Task<List<ActionTrigger>> GetByTriggerIdAsync(int triggerId);
        Task<ActionTrigger> AddAsync(ActionTrigger actionTrigger);
        Task DeleteAsync(int id);
        Task DeleteByActionAndTriggerAsync(int actionId, int triggerId);
        Task<bool> ExistsAsync(int actionId, int triggerId);
    }
}
