using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Commands;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ToggleCommandDisabledHandler(ILogger<ToggleCommandDisabledHandler> logger, IActionCommandService commandService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ToggleCommandDisabledState;

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if(subAction is not ToggleCommandDisabledType toggleCommandDisabled)
            {
                logger.LogError("Invalid sub action type provided to ToggleCommandDisabledHandler");
                return;
            }

            if(string.IsNullOrWhiteSpace(toggleCommandDisabled.CommandName))
            {
                logger.LogError("No command name provided for ToggleCommandDisabledHandler");
                return;
            }

            var command = await commandService.GetByCommandNameAsync(toggleCommandDisabled.CommandName);
            if (command == null)
            {
                logger.LogError("No command found with name '{CommandName}' for ToggleCommandDisabledHandler", toggleCommandDisabled.CommandName);
                return;
            }

            command.Disabled = toggleCommandDisabled.IsDisabled;
            await commandService.UpdateAsync(command);
        }
    }
}
