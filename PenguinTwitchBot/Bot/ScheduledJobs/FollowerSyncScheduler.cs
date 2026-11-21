using PenguinTwitchBot.Bot.Core;

namespace PenguinTwitchBot.Bot.ScheduledJobs
{
    public sealed class FollowerSyncScheduler(
        ILogger<FollowerSyncScheduler> logger,
        IFollowTracker followTracker) : BackgroundService
    {
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(StartupDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            await CheckAndRunAsync(stoppingToken);

            using var timer = new PeriodicTimer(PollInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CheckAndRunAsync(stoppingToken);
            }
        }

        private async Task CheckAndRunAsync(CancellationToken stoppingToken)
        {
            try
            {
                await followTracker.TryRunScheduledSyncIfDueAsync(cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled follower sync failed.");
            }
        }
    }
}
