using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Actions.SubActions;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ResetCooldownsHandler(ICommandHandler commandHandler) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ResetCooldowns;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not ResetCooldownsType resetCooldowns)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type provided to ResetCooldownsHandler");
            }

            var commandName = VariableReplacer.ReplaceVariables(resetCooldowns.CommandName, variables);
            if (string.IsNullOrWhiteSpace(commandName) && variables.TryGetValue(ActionExecutionVariableKeys.CooldownCommandName, out var queuedCommandName))
            {
                commandName = queuedCommandName;
            }

            if (string.IsNullOrWhiteSpace(commandName))
            {
                throw new SubActionHandlerException(subAction, "No command name provided for ResetCooldownsHandler");
            }

            var userName = VariableReplacer.ReplaceVariables(resetCooldowns.UserName, variables);
            if (string.IsNullOrWhiteSpace(userName) && variables.TryGetValue(ActionExecutionVariableKeys.CooldownUserName, out var queuedUserName))
            {
                userName = queuedUserName;
            }

            await commandHandler.ResetCooldownsForCommand(
                commandName,
                userName,
                resetCooldowns.ResetUserCooldown,
                resetCooldowns.ResetGlobalCooldown);

            context?.LogMessage(subActionIndex,
                $"Reset cooldowns for {commandName} (user: {resetCooldowns.ResetUserCooldown}, global: {resetCooldowns.ResetGlobalCooldown})");
        }
    }
}