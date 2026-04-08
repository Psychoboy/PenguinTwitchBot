using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Commands;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ToggleCommandDisabledHandler(IActionCommandService commandService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ToggleCommandDisabledState;

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if(subAction is not ToggleCommandDisabledType toggleCommandDisabled)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type provided to ToggleCommandDisabledHandler");
            }

            if(string.IsNullOrWhiteSpace(toggleCommandDisabled.CommandName))
            {
                throw new SubActionHandlerException(subAction, "No command name provided for ToggleCommandDisabledHandler");
            }

            var command = await commandService.GetByCommandNameAsync(toggleCommandDisabled.CommandName) ??
                throw new SubActionHandlerException(subAction, "No command found with name '{CommandName}' for ToggleCommandDisabledHandler", toggleCommandDisabled.CommandName);
            command.Disabled = toggleCommandDisabled.IsDisabled;
            await commandService.UpdateAsync(command);
        }
    }
}
