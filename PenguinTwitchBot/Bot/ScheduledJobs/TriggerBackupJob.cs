using PenguinTwitchBot.Bot.Core.Database;
using Quartz;

namespace PenguinTwitchBot.Bot.ScheduledJobs
{
    [DisallowConcurrentExecution]
    public class TriggerBackupJob(IDatabaseTools databaseTools, ILogger<TriggerBackupJob> logger) : IJob
    {
        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Scheduled backup job started.");
            await databaseTools.Backup();
            logger.LogInformation("Scheduled backup job completed.");
        }
    }
}
