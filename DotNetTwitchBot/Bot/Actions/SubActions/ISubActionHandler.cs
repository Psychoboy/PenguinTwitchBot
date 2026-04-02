using DotNetTwitchBot.Bot.Models.Actions.SubActions;

namespace DotNetTwitchBot.Bot.Actions.SubActions
{
    public interface ISubActionHandler
    {
        SubActionTypes SupportedType { get; }
        Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables);
    }
}
