using DotNetTwitchBot.Bot.Models.Actions.SubActions;

namespace DotNetTwitchBot.Bot.Actions.SubActions
{
    public class SubActionHandlerFactory(IEnumerable<ISubActionHandler> handlers, ILogger<SubActionHandlerFactory> logger)
    {
        private readonly Dictionary<SubActionTypes, ISubActionHandler> _handlers = handlers.ToDictionary(h => h.SupportedType);

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (_handlers.TryGetValue(subAction.SubActionTypes, out var handler))
            {
                if(!subAction.Enabled)
                {
                    logger.LogInformation("Sub action {subAction.Text} was disabled so skipping", subAction.Text);
                    return;
                }
                await handler.ExecuteAsync(subAction, variables);
            }
            else
            {
                logger.LogWarning("No handler found for SubActionType: {SubActionType}", subAction.SubActionTypes);
            }
        }
    }
}
