namespace DotNetTwitchBot.Bot.Actions
{
    public interface IAction
    {
        Task<ActionType> AddAction(ActionType action);
        Task EnqueueAction(Dictionary<string, string> variables, ActionType action, Guid? parentLogId = null, int? parentSubActionIndex = null);
        Task RunAction(Dictionary<string, string> variables, ActionType action);
    }
}