using PenguinTwitchBot.Services;
using Quartz;

namespace PenguinTwitchBot.Bot.ScheduledJobs
{
    [DisallowConcurrentExecution]
    public class CleanupClipsJob(IFileCleanupService fileCleanupService, ILogger<CleanupClipsJob> logger) : IJob
    {
        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            logger.LogDebug("Scheduled clips cleanup job started.");
            await fileCleanupService.CleanupClipsAsync();
            logger.LogDebug("Scheduled clips cleanup job completed.");
        }
    }
}
