using PenguinTwitchBot.Database.Bot.Models.Actions;
using PenguinTwitchBot.Database.Bot.Models.Timers;

namespace PenguinTwitchBot.Bot.Commands.Misc
{
    public interface IAutoTimers
    {
        Task<List<TimerGroup>> GetTimerGroupsAsync();
        Task<TimerGroup?> GetTimerGroupAsync(int id);
        Task AddTimerGroup(TimerGroup group);
        Task UpdateTimerGroup(TimerGroup group, string oldName, string newName);
        Task UpdateTimerGroup(TimerGroup group);
        Task DeleteTimerGroup(TimerGroup group);
        Task RunGroup(TimerGroup group, bool overrideCheck = false);
        Task<TimerGroup> UpdateNextRun(TimerGroup group);
        Task<List<ActionType>> GetActionsForTimerGroup(int timerGroupId);
        Task AddActionToTimerGroup(int timerGroupId, int actionId);
        Task RemoveActionFromTimerGroup(int timerGroupId, int actionId);
        Task OnChatMessage();
    }
}
