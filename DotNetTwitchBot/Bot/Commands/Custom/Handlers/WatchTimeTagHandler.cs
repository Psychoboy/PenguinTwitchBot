using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.Commands.Features;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class WatchTimeTagHandler(ILoyaltyFeature loyaltyFeature) : IRequestHandler<WatchTimeTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(WatchTimeTag request, CancellationToken cancellationToken)
        {
            var time = await loyaltyFeature.GetViewerWatchTime(request.Args);
            return new CustomCommandResult(time);
        }
    }
}
