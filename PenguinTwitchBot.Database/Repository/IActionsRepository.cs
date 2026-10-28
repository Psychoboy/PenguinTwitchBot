using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IActionsRepository : IGenericRepository<ActionType>
    {
        Task<ActionType?> GetByIdWithDetailsAsync(Guid id);
        Task<List<ActionType>> GetAllWithDetailsAsync();
        /// <summary>Actions without sub-actions or triggers, ordered by name; use when only names/ids are needed.</summary>
        Task<List<ActionType>> GetAllSummariesAsync();
        Task<ActionType> CreateActionAsync(ActionType action);
        Task<ActionType> UpdateActionAsync(ActionType action);
        Task DeleteActionAsync(Guid id);
        Task<List<ActionType>> GetActionsByTriggerTypeAndNameAsync(TriggerTypes triggerType, string triggerName);
        /// <summary>Actions with an enabled trigger referencing the command ID.</summary>
        Task<List<ActionType>> GetActionsByCommandIdAsync(Guid commandId);
        /// <summary>Actions with an enabled trigger referencing the keyword ID.</summary>
        Task<List<ActionType>> GetActionsByKeywordIdAsync(Guid keywordId);
        Task UpdateToggleCommandDisabledNamesForRenamedCommand(string oldCommandName, string newCommandName);
    }
}
