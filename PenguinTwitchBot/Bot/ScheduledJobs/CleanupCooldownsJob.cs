using PenguinTwitchBot.Services;
using Quartz;

namespace PenguinTwitchBot.Bot.ScheduledJobs
{
    [DisallowConcurrentExecution]
    public class CleanupCooldownsJob(ICooldownCleanupService cooldownCleanupService, ILogger<CleanupCooldownsJob> logger) : IJob
    {
        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            logger.LogDebug("Scheduled job to clean expired cooldowns started.");
            var removed = await cooldownCleanupService.CleanupExpiredCooldownsAsync();
            logger.LogDebug("Scheduled job to clean expired cooldowns completed. Removed {RemovedCooldowns} rows.", removed);
        }
    }
}