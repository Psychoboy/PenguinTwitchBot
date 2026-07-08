using PenguinTwitchBot.Services;
using Quartz;

namespace PenguinTwitchBot.Bot.ScheduledJobs
{
    [DisallowConcurrentExecution]
    public class TtsCleanupJob(IFileCleanupService fileCleanupService, ILogger<TtsCleanupJob> logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            logger.LogDebug("Scheduled TTS cleanup job started.");
            await fileCleanupService.CleanupTtsAsync();
            logger.LogDebug("Scheduled TTS cleanup job completed.");
        }
    }
}
