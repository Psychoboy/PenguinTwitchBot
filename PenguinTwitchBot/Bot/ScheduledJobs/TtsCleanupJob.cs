using PenguinTwitchBot.Services;
using Quartz;

namespace PenguinTwitchBot.Bot.ScheduledJobs
{
    [DisallowConcurrentExecution]
    public class TtsCleanupJob(IFileCleanupService fileCleanupService, ILogger<TtsCleanupJob> logger) : IJob
    {
        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            logger.LogDebug("Scheduled TTS cleanup job started.");
            await fileCleanupService.CleanupTtsAsync();
            logger.LogDebug("Scheduled TTS cleanup job completed.");
        }
    }
}
