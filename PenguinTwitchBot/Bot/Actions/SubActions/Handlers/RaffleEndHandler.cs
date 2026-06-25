using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using PenguinTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class RaffleEndHandler(IRaffleRuntimeService raffleRuntimeService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.RaffleEnd;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not RaffleEndType raffleEnd)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for RaffleEndHandler: {SubActionType}", subAction.GetType().Name);
            }

            var raffleKey = VariableReplacer.ReplaceVariables(raffleEnd.RaffleKey, variables);
            var result = await raffleRuntimeService.EndAsync(raffleKey);
            RaffleSubActionVariableWriter.Write(variables, result);
        }
    }
}