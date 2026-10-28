using PenguinTwitchBot.Database.Bot.Models.Actions;
using PenguinTwitchBot.Database.Bot.Models.Timers;

namespace PenguinTwitchBot.Bot.Commands.Misc
{
    public interface IAutoTimers
    {
        Task<List<TimerGroup>> GetTimerGroupsAsync();
        Task<TimerGroup?> GetTimerGroupAsync(Guid id);
        Task AddTimerGroup(TimerGroup group);
        Task UpdateTimerGroup(TimerGroup group, string oldName, string newName);
        Task UpdateTimerGroup(TimerGroup group);
        Task DeleteTimerGroup(TimerGroup group);
        Task RunGroup(TimerGroup group, bool overrideCheck = false);
        Task<TimerGroup> UpdateNextRun(TimerGroup group);
        Task<List<ActionType>> GetActionsForTimerGroup(Guid timerGroupId);
        Task AddActionToTimerGroup(Guid timerGroupId, Guid actionId);
        Task RemoveActionFromTimerGroup(Guid timerGroupId, Guid actionId);
        Task OnChatMessage();
    }
}
