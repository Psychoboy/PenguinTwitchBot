using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using PenguinTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class RaffleGetEntryCountHandler(IRaffleRuntimeService raffleRuntimeService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.RaffleGetEntryCount;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not RaffleGetEntryCountType raffleGetEntryCount)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for RaffleGetEntryCountHandler: {SubActionType}", subAction.GetType().Name);
            }

            var raffleKey = VariableReplacer.ReplaceVariables(raffleGetEntryCount.RaffleKey, variables);
            var result = await raffleRuntimeService.GetEntryCountAsync(raffleKey);
            RaffleSubActionVariableWriter.Write(variables, result);
        }
    }
}