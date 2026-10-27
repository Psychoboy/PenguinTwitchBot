using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IActionsRepository : IGenericRepository<ActionType>
    {
        Task<ActionType?> GetByIdWithDetailsAsync(Guid id);
        Task<List<ActionType>> GetAllWithDetailsAsync();
        Task<ActionType> CreateActionAsync(ActionType action);
        Task<ActionType> UpdateActionAsync(ActionType action);
        Task DeleteActionAsync(Guid id);
        Task<List<ActionType>> GetActionsByTriggerTypeAndNameAsync(TriggerTypes triggerType, string triggerName);
        Task UpdateToggleCommandDisabledNamesForRenamedCommand(string oldCommandName, string newCommandName);
    }
}
