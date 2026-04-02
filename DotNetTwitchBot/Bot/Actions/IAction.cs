using DotNetTwitchBot.Bot.Models.Actions;

namespace DotNetTwitchBot.Bot.Actions
{
    public interface IAction
    {
        Task<ActionType> AddAction(ActionType action);
        Task EnqueueAction(Dictionary<string, string> variables, ActionType action);
        Task RunAction(Dictionary<string, string> variables, ActionType action);
    }
}