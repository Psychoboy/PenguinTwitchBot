using PenguinTwitchBot.Services;
using Quartz;

namespace PenguinTwitchBot.Bot.ScheduledJobs
{
    [DisallowConcurrentExecution]
    public class RefreshEmoteCacheJob(IEmoteService emoteService, ILogger<RefreshEmoteCacheJob> logger) : IJob
    {
        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // Populates the shared cache so the first page or overlay that needs emotes doesn't wait on the upstream APIs.
                var emotes = await emoteService.GetEmotesAsync();
                logger.LogDebug("Emote cache warmed with {Count} emotes.", emotes.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Emote cache refresh failed.");
            }
        }
    }
}
