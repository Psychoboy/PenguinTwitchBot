using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Commands.Features;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class GiveawayPrizeHandler(GiveawayFeature giveawayFeature, ILogger<GiveawayPrizeHandler> logger) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.GiveawayPrize;

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not GiveawayPrizeType)
            {
                logger.LogError("Invalid sub action type for GiveawayPrizeHandler: {SubActionType}", subAction.GetType().Name);
                return;
            }

            var prize = await giveawayFeature.GetPrize();
            variables["Prize"] = prize ?? string.Empty;
        }
    }
}
