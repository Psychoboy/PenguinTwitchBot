using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ToggleCommandDisabledHandler(IActionCommandService commandService, ISubActionExecutionContextAccessor contextAccessor) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ToggleCommandDisabledState;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables)
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
                var context = contextAccessor.ExecutionContext;
                var state = toggleCommandDisabled.IsDisabled ? "disabled" : "enabled";
                context?.LogMessage(contextAccessor.CurrentSubActionIndex, $"Command {command.CommandName} (ID: {command.Id}) set to {state}");
        }
    }
}
