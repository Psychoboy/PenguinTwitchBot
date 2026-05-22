using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Repository
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
        Task UpdateKeywordTriggerConfigurationsForRenamedKeyword(int keywordId, string oldKeywordName, string newKeywordName);
        Task UpdateTimerGroupNamesForRenamedTimerGroup(int timerGroupId, string newName);
        Task UpdateToggleCommandDisabledNamesForRenamedCommand(string oldCommandName, string newCommandName);
        Task RemapEntityReferencesAfterRestore(ILogger? logger = null);
    }
}
