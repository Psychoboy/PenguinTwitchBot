using PenguinTwitchBot.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using PenguinTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class RaffleStartHandler(IRaffleRuntimeService raffleRuntimeService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.RaffleStart;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not RaffleStartType raffleStart)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for RaffleStartHandler: {SubActionType}", subAction.GetType().Name);
            }

            var totalAwardText = VariableReplacer.ReplaceVariables(raffleStart.TotalAward, variables);
            if (!long.TryParse(totalAwardText, out var totalAward) || totalAward < 0)
            {
                throw new SubActionHandlerException(subAction, "Invalid Total Award value for RaffleStart: {TotalAward}", totalAwardText);
            }

            var result = await raffleRuntimeService.StartAsync(new RaffleStartRequest
            {
                RaffleKey = VariableReplacer.ReplaceVariables(raffleStart.RaffleKey, variables),
                RaffleName = VariableReplacer.ReplaceVariables(raffleStart.RaffleName, variables),
                JoinCommand = VariableReplacer.ReplaceVariables(raffleStart.JoinCommand, variables),
                PointGameName = VariableReplacer.ReplaceVariables(raffleStart.PointGameName, variables),
                WinnerCount = raffleStart.WinnerCount,
                TotalAward = totalAward
            });

            RaffleSubActionVariableWriter.Write(variables, result);
        }
    }
}