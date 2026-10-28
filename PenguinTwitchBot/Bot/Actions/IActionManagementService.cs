using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions
{
    public interface IActionManagementService
    {
        Task<List<ActionType>> GetAllActionsAsync();
        /// <summary>Actions without sub-actions or triggers; much cheaper than <see cref="GetAllActionsAsync"/>.</summary>
        Task<List<ActionType>> GetActionSummariesAsync();
        Task<ActionType?> GetActionByIdAsync(Guid id);
        Task<ActionType> CreateActionAsync(ActionType action);
        Task<ActionType> UpdateActionAsync(ActionType action);
        Task DeleteActionAsync(Guid id);
        Task<List<ActionType>> GetActionsByTriggerTypeAndNameAsync(TriggerTypes triggerType, string triggerName);
        Task<List<ActionType>> GetActionsByTriggerTypeAndNameEnabledAsync(TriggerTypes triggerType, string triggerName);
        /// <summary>Enabled actions with an enabled trigger for this command, matched by id (name only for legacy triggers without one).</summary>
        Task<List<ActionType>> GetActionsByCommandIdEnabledAsync(Guid commandId, string legacyTriggerName);
        /// <summary>Actions (enabled or not) with an enabled trigger for this keyword, matched by id.</summary>
        Task<List<ActionType>> GetActionsByKeywordIdAsync(Guid keywordId, string legacyTriggerName);
        /// <summary>Enabled actions with an enabled trigger for this keyword, matched by id (name only for legacy triggers without one).</summary>
        Task<List<ActionType>> GetActionsByKeywordIdEnabledAsync(Guid keywordId, string legacyTriggerName);
        /// <summary>Keeps trigger names in step with the command, keyword or timer group they point at.</summary>
        Task RenameTriggersAsync(TriggerTypes triggerType, Guid referenceId, string newName);

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
