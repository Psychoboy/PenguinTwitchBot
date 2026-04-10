using DotNetTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions
{
    public interface IAction
    {
        Task<ActionType> AddAction(ActionType action);
        Task EnqueueAction(ConcurrentDictionary<string, string> variables, ActionType action, Guid? parentLogId = null, int? parentSubActionIndex = null);
        Task RunAction(ConcurrentDictionary<string, string> variables, ActionType action, ActionExecutionContext? context = null);
    }
}