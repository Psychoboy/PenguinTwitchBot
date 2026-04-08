using DotNetTwitchBot.Bot.StreamSchedule;
using Quartz;

namespace DotNetTwitchBot.Bot.ScheduledJobs
{
    public class UpdatePostedSchedule(ISchedule schedule) : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            return schedule.UpdatePostedSchedule();
        }
    }
}
