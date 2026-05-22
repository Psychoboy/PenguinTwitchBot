using PenguinTwitchBot.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Bot.Commands.Features;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class GiveawayPrizeHandler(GiveawayFeature giveawayFeature) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.GiveawayPrize;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not GiveawayPrizeType)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for GiveawayPrizeHandler: {SubActionType}", subAction.GetType().Name);
            }

            var prize = await giveawayFeature.GetPrize();
            variables["Prize"] = prize ?? string.Empty;
        }
    }
}

