using PenguinTwitchBot.Bot.Core.Database;
using Quartz;

namespace PenguinTwitchBot.Bot.ScheduledJobs
{
    [DisallowConcurrentExecution]
    public class TriggerBackupJob(IDatabaseTools databaseTools, ILogger<TriggerBackupJob> logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            logger.LogInformation("Scheduled backup job started.");
            await databaseTools.Backup();
            logger.LogInformation("Scheduled backup job completed.");
        }
    }
}
