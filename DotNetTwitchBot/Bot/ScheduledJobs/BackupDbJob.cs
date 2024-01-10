using Quartz;

namespace DotNetTwitchBot.Bot.ScheduledJobs
{
    [DisallowConcurrentExecution]
    public class BackupDbJob(ILogger<BackupDbJob> logger, IDatabaseTools databaseTools) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            logger.LogInformation("DB Backup Starting");
            await databaseTools.Backup();
            logger.LogInformation("DB Backup Complete");
        }
    }
}