using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.Commands.Misc;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class EnableTimerGroupTagHandler(ILogger<EnableTimerGroupTagHandler> logger, AutoTimers autoTimers) : IRequestHandler<EnableTimerGroupTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(EnableTimerGroupTag request, CancellationToken cancellationToken)
        {
            var args = request.Args;
            logger.LogInformation("Trying to enable {name} title", args);
            var timers = await autoTimers.GetTimerGroupsAsync();
            var timerGroup = timers.Where(x => x.Name.Equals(args.Trim(), StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (timerGroup != null)
            {
                timerGroup = await autoTimers.UpdateNextRun(timerGroup);
                timerGroup.Active = true;
                await autoTimers.UpdateTimerGroup(timerGroup);
                logger.LogInformation("Enabled {name} timer.", args);
            }
            return new CustomCommandResult();
        }
    }
}
