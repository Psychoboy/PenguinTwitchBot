using PenguinTwitchBot.Bot.StreamSchedule;
using Quartz;

namespace PenguinTwitchBot.Bot.ScheduledJobs
{
    [DisallowConcurrentExecution]
    public class UpdateDiscordEvents(ISchedule schedule) : IJob
    {
        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            await schedule.UpdateEvents();
        }
    }
}
