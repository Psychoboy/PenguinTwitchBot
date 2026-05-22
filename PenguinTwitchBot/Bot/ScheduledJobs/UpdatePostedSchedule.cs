using PenguinTwitchBot.Bot.StreamSchedule;
using Quartz;

namespace PenguinTwitchBot.Bot.ScheduledJobs
{
    public class UpdatePostedSchedule(ISchedule schedule) : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            return schedule.UpdatePostedSchedule();
        }
    }
}
