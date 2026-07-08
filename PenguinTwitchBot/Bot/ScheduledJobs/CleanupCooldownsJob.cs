using PenguinTwitchBot.Services;
using Quartz;

namespace PenguinTwitchBot.Bot.ScheduledJobs
{
    [DisallowConcurrentExecution]
    public class CleanupCooldownsJob(ICooldownCleanupService cooldownCleanupService, ILogger<CleanupCooldownsJob> logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            logger.LogDebug("Scheduled job to clean expired cooldowns started.");
            var removed = await cooldownCleanupService.CleanupExpiredCooldownsAsync();
            logger.LogDebug("Scheduled job to clean expired cooldowns completed. Removed {RemovedCooldowns} rows.", removed);
        }
    }
}