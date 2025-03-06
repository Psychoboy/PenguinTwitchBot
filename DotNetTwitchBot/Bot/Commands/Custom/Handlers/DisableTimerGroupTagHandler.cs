using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.Commands.Misc;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class DisableTimerGroupTagHandler(AutoTimers autoTimers) : IRequestHandler<DisableTimerGroupTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(DisableTimerGroupTag request, CancellationToken cancellationToken)
        {
            var args = request.Args;
            var timers = await autoTimers.GetTimerGroupsAsync();
            var timerGroup = timers.Where(x => x.Name.Equals(args.Trim(), StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (timerGroup != null)
            {
                timerGroup.Active = false;
                await autoTimers.UpdateTimerGroup(timerGroup);
            }
            return new CustomCommandResult();
        }
    }
}
