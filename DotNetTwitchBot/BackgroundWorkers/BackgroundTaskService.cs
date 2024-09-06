namespace DotNetTwitchBot.BackgroundWorkers
{
    public class BackgroundTaskService(IBackgroundTaskQueue taskQueue, ILogger<BackgroundTaskService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var workItem = await taskQueue.DequeueAsync(stoppingToken);

                try
                {
                    if (workItem != null)
                    {
                        await workItem(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing background Queue.");
                }
            }
        }
    }
}
