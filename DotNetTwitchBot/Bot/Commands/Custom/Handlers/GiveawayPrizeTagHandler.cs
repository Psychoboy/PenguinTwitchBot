using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.Commands.Features;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class GiveawayPrizeTagHandler(GiveawayFeature giveawayFeature) : IRequestHandler<GiveawayPrizeTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(GiveawayPrizeTag request, CancellationToken cancellationToken)
        {
            var prize = await giveawayFeature.GetPrize();
            return new CustomCommandResult(prize);
        }
    }
}
