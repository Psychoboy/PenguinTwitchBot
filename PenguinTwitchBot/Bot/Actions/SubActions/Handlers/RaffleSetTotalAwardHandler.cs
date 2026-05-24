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
            var totalAwardText = VariableReplacer.ReplaceVariables(raffleSetTotalAward.TotalAward, variables);

            if (!long.TryParse(totalAwardText, out var totalAward) || totalAward < 0)
            {
                throw new SubActionHandlerException(subAction, "Invalid Total Award value for RaffleSetTotalAward: {TotalAward}", totalAwardText);
            }

            var result = await raffleRuntimeService.SetTotalAwardAsync(raffleKey, totalAward);
            RaffleSubActionVariableWriter.Write(variables, result);
        }
    }
}