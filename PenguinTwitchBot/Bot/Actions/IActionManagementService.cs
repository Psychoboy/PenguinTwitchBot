using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions
{
    public interface IActionManagementService
    {
        Task<List<ActionType>> GetAllActionsAsync();
        Task<ActionType?> GetActionByIdAsync(Guid id);
        Task<ActionType> CreateActionAsync(ActionType action);
        Task<ActionType> UpdateActionAsync(ActionType action);
        Task DeleteActionAsync(Guid id);
        Task<List<ActionType>> GetActionsByTriggerTypeAndNameAsync(TriggerTypes triggerType, string triggerName);
        Task<List<ActionType>> GetActionsByTriggerTypeAndNameEnabledAsync(TriggerTypes triggerType, string triggerName);

        // Trigger management (Triggers are now children of Actions)
        Task<List<TriggerType>> GetTriggersForActionAsync(Guid actionId);
        Task<List<TriggerType>> GetAllTriggersAsync();
        Task<TriggerType?> GetTriggerByIdAsync(Guid id);
        Task<TriggerType> CreateTriggerAsync(TriggerType trigger);
        Task<TriggerType> UpdateTriggerAsync(TriggerType trigger);
        Task DeleteTriggerAsync(Guid triggerId);
        Task DeleteTriggersForCommandAsync(Guid commandId);
        Task DeleteTriggersForKeywordAsync(Guid keywordId);
    }
}
