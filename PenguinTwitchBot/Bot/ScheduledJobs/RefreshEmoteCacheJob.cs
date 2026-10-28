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
                var emotes = await emoteService.RefreshEmotesAsync();
                logger.LogInformation("Emote cache refreshed with {Count} emotes.", emotes.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Emote cache refresh failed.");
            }
        }
    }
}
