using PenguinTwitchBot.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class BreakHandler() : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.Break;

        public Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not BreakType)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type Break is not of BreakType class");
            }
            context?.LogMessage(subActionIndex, "BreakHandler: Executing BreakType sub-action, throwing BreakException to break out of current action execution.");
            throw new BreakException(); // This will be caught by the ActionExecutor and used to break out of the current action execution
        }
    }

}
