using Quartz;

namespace DotNetTwitchBot.Bot.ScheduledJobs
{
    [DisallowConcurrentExecution]
    public class BackupDbJob(IDatabaseTools databaseTools) : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            return databaseTools.Backup();
        }
    }
}