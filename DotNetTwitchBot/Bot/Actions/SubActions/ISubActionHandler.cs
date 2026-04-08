using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions
{
    public interface ISubActionHandler
    {
        SubActionTypes SupportedType { get; }
        Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables);
    }
}
