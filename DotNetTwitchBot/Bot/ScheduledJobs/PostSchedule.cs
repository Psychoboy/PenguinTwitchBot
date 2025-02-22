﻿using DotNetTwitchBot.Bot.StreamSchedule;
using Quartz;

namespace DotNetTwitchBot.Bot.ScheduledJobs
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
