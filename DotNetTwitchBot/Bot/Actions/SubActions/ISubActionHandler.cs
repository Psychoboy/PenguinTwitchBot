using DotNetTwitchBot.Bot.Actions.SubActions.Types;

namespace DotNetTwitchBot.Bot.Actions.SubActions
{
    public interface ISubActionHandler
    {
        SubActionTypes SupportedType { get; }
        Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables);
    }
}
