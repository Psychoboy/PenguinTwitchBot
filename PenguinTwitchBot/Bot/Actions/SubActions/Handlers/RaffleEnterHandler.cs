using PenguinTwitchBot.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using PenguinTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class RaffleEnterHandler(IRaffleRuntimeService raffleRuntimeService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.RaffleEnter;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not RaffleEnterType raffleEnter)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for RaffleEnterHandler: {SubActionType}", subAction.GetType().Name);
            }

            var raffleKey = VariableReplacer.ReplaceVariables(raffleEnter.RaffleKey, variables);
            var eventArgs = CommandEventArgsConverter.FromDictionaryOrNull(variables);
            var username = eventArgs?.Name ?? variables.GetValueOrDefault("Name") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new SubActionHandlerException(subAction, "Raffle Enter requires a command user context.");
            }

            var result = await raffleRuntimeService.EnterAsync(raffleKey, username);
            RaffleSubActionVariableWriter.Write(variables, result);
        }
    }
}