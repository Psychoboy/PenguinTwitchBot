using PenguinTwitchBot.Bot.StreamSchedule;
using Quartz;

namespace PenguinTwitchBot.Bot.ScheduledJobs
{
    [DisallowConcurrentExecution]
    public class PostSchedule(ISchedule schedule) : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            return schedule.PostSchedule();
        }
    }
}
