using DotNetTwitchBot.Bot.Actions.SubActions.Types;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class BreakHandler() : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.Break;

        public Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not BreakType)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type Break is not of BreakType class");
            }
            throw new BreakException(); // This will be caught by the ActionExecutor and used to break out of the current action execution
        }
    }

}
