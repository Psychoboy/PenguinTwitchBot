using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.TwitchServices;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class UptimeTagHandler(ITwitchService twitchService) : IRequestHandler<UptimeTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(UptimeTag request, CancellationToken cancellationToken)
        {
            var streamTime = await twitchService.StreamStartedAt();
            if (streamTime == DateTime.MinValue) return new CustomCommandResult("Stream is offline");
            var currentTime = DateTime.UtcNow;
            var totalTime = currentTime - streamTime;
            return new CustomCommandResult(totalTime.ToString(@"hh\:mm\:ss"));
        }
    }
}
