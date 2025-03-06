using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.TwitchServices;
using MediatR;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class EnableChannelPointTagHandler(ILogger<EnableChannelPointTagHandler> logger, ITwitchService twitchService) : IRequestHandler<EnableChannelPointTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(EnableChannelPointTag request, CancellationToken cancellationToken)
        {
            var args = request.Args;
            logger.LogInformation("Trying to enable {title} channel point.", args);
            var channelPoints = await twitchService.GetChannelPointRewards(true);
            foreach (var channelPoint in channelPoints)
            {
                if (channelPoint.Title.Equals(args.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    UpdateCustomRewardRequest updateRequest = new()
                    {
                        IsEnabled = true
                    };
                    logger.LogInformation("Channel point {title} enabled.", channelPoint.Title);
                    await twitchService.UpdateChannelPointReward(channelPoint.Id, updateRequest);
                    break;
                }
            }
            return new CustomCommandResult();
        }
    }
}
