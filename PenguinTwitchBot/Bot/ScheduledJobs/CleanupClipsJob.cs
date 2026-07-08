using PenguinTwitchBot.Services;
using Quartz;

namespace PenguinTwitchBot.Bot.ScheduledJobs
{
    [DisallowConcurrentExecution]
    public class CleanupClipsJob(IFileCleanupService fileCleanupService, ILogger<CleanupClipsJob> logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            logger.LogDebug("Scheduled clips cleanup job started.");
            await fileCleanupService.CleanupClipsAsync();
            logger.LogDebug("Scheduled clips cleanup job completed.");
        }
    }
}
