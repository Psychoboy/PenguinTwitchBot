using DotNetTwitchBot.Bot.Actions;
using DotNetTwitchBot.Bot.Models.Actions.Triggers;

namespace DotNetTwitchBot.Repository
{
    public interface IActionsRepository : IGenericRepository<ActionType>
    {
        Task<ActionType?> GetByIdWithDetailsAsync(int id);
        Task<List<ActionType>> GetAllWithDetailsAsync();
        Task<ActionType> CreateActionAsync(ActionType action);
        Task<ActionType> UpdateActionAsync(ActionType action);
        Task DeleteActionAsync(int id);
        Task<List<ActionType>> GetActionsByTriggerTypeAndNameAsync(TriggerTypes triggerType, string triggerName);
        Task UpdateExecuteActionNamesForRenamedAction(int actionId, string newName);
        Task UpdateCommandTriggerConfigurationsForRenamedCommand(int commandId, string oldCommandName, string newCommandName);
        Task UpdateTimerGroupNamesForRenamedTimerGroup(int timerGroupId, string newName);
        Task UpdateToggleCommandDisabledNamesForRenamedCommand(string oldCommandName, string newCommandName);
        Task RemapEntityReferencesAfterRestore(ILogger? logger = null);
    }
}
