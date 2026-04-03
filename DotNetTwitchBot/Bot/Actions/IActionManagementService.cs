using DotNetTwitchBot.Bot.Models.Actions.Triggers;

namespace DotNetTwitchBot.Bot.Actions
{
    public interface IActionManagementService
    {
        Task<List<ActionType>> GetAllActionsAsync();
        Task<ActionType?> GetActionByIdAsync(int id);
        Task<ActionType> CreateActionAsync(ActionType action);
        Task<ActionType> UpdateActionAsync(ActionType action);
        Task DeleteActionAsync(int id);
        Task<List<ActionType>> GetActionsByTriggerTypeAndNameAsync(TriggerTypes triggerType, string triggerName);

        // Trigger management (Triggers are now children of Actions)
        Task<List<TriggerType>> GetTriggersForActionAsync(int actionId);
        Task<List<TriggerType>> GetAllTriggersAsync();
        Task<TriggerType?> GetTriggerByIdAsync(int id);
        Task<TriggerType> CreateTriggerAsync(TriggerType trigger);
        Task<TriggerType> UpdateTriggerAsync(TriggerType trigger);
        Task DeleteTriggerAsync(int triggerId);
        Task DeleteTriggersForCommandAsync(int commandId);
    }
}
