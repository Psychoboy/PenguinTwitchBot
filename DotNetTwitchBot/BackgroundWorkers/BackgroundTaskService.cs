namespace DotNetTwitchBot.BackgroundWorkers
{
    public class BackgroundTaskService(IBackgroundTaskQueue taskQueue,
    ILogger<BackgroundTaskService> logger) : BackgroundService
    {
        public IBackgroundTaskQueue TaskQueue { get; } = taskQueue;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation(
                "Queued Hosted Service is running.");

            await BackgroundProcessing(stoppingToken);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var workItem =
                        await TaskQueue.DequeueAsync(stoppingToken);
                        await workItem(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex,
                            "Error occurred executing Task.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred background task.");
            }
            logger.LogWarning("Background task is stopping.");
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Queued Hosted Service is stopping.");

            await base.StopAsync(stoppingToken);
        }
    }
}
