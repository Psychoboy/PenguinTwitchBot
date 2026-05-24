using PenguinTwitchBot.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using PenguinTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class RaffleSetTotalAwardHandler(IRaffleRuntimeService raffleRuntimeService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.RaffleSetTotalAward;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not RaffleSetTotalAwardType raffleSetTotalAward)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for RaffleSetTotalAwardHandler: {SubActionType}", subAction.GetType().Name);
            }

            var raffleKey = VariableReplacer.ReplaceVariables(raffleSetTotalAward.RaffleKey, variables);
            var result = await raffleRuntimeService.SetTotalAwardAsync(raffleKey, raffleSetTotalAward.TotalAward);
            RaffleSubActionVariableWriter.Write(variables, result);
        }
    }
}