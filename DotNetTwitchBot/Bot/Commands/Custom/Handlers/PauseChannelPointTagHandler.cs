using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.TwitchServices;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class PauseChannelPointTagHandler(ITwitchService twitchService) : IRequestHandler<PauseChannelPointTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(PauseChannelPointTag request, CancellationToken cancellationToken)
        {
            var args = request.Args;
            var channelPoints = await twitchService.GetChannelPointRewards(true);
            foreach (var channelPoint in channelPoints)
            {
                if (channelPoint.Title.Equals(args, StringComparison.OrdinalIgnoreCase))
                {
                    TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward.UpdateCustomRewardRequest requestUpdate = new()
                    {
                        IsPaused = true
                    };
                    await twitchService.UpdateChannelPointReward(channelPoint.Id, requestUpdate);
                    break;
                }
            }
            return new CustomCommandResult();
        }
    }
}
