using DotNetTwitchBot.Bot.Models.Actions;
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

        Task<List<ActionTrigger>> GetTriggersForActionAsync(int actionId);
        Task<ActionTrigger> AddTriggerToActionAsync(ActionTrigger actionTrigger);
        Task RemoveTriggerFromActionAsync(int actionTriggerId);

        // Trigger management
        Task<List<TriggerType>> GetAllTriggersAsync();
        Task<TriggerType?> GetTriggerByIdAsync(int id);
        Task<TriggerType> CreateTriggerAsync(TriggerType trigger);
        Task<TriggerType> UpdateTriggerAsync(TriggerType trigger);
    }
}
