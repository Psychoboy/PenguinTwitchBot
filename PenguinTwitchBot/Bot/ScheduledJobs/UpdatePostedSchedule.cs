using PenguinTwitchBot.Bot.StreamSchedule;
using Quartz;

namespace PenguinTwitchBot.Bot.ScheduledJobs
{
    public class UpdatePostedSchedule(ISchedule schedule) : IJob
    {
        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            await schedule.UpdatePostedSchedule();
        }
    }
}
