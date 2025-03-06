using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.TwitchServices;
using MediatR;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class DisableChannelPointTagHandler(ITwitchService twitchService) : IRequestHandler<DisableChannelPointTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(DisableChannelPointTag request, CancellationToken cancellationToken)
        {
            var args = request.Args;
            var channelPoints = await twitchService.GetChannelPointRewards(true);
            foreach (var channelPoint in channelPoints)
            {
                if (channelPoint.Title.Equals(args, StringComparison.OrdinalIgnoreCase))
                {
                    UpdateCustomRewardRequest updateRequest = new()
                    {
                        IsEnabled = false
                    };
                    await twitchService.UpdateChannelPointReward(channelPoint.Id, updateRequest);
                    break;
                }
            }
            return new CustomCommandResult();
        }
    }
}
