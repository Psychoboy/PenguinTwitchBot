using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using PenguinTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class RaffleSetWinnerCountHandler(IRaffleRuntimeService raffleRuntimeService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.RaffleSetWinnerCount;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not RaffleSetWinnerCountType raffleSetWinnerCount)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for RaffleSetWinnerCountHandler: {SubActionType}", subAction.GetType().Name);
            }

            var raffleKey = VariableReplacer.ReplaceVariables(raffleSetWinnerCount.RaffleKey, variables);
            var result = await raffleRuntimeService.SetWinnerCountAsync(raffleKey, raffleSetWinnerCount.WinnerCount);
            RaffleSubActionVariableWriter.Write(variables, result);
        }
    }
}