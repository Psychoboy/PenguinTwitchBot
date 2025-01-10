namespace DotNetTwitchBot.BackgroundWorkers
{
    public class BackgroundTaskService : BackgroundService
    {
        private readonly ILogger<BackgroundTaskService> _logger;
        public BackgroundTaskService(IBackgroundTaskQueue taskQueue,
        ILogger<BackgroundTaskService> logger)
        {
            TaskQueue = taskQueue;
            _logger = logger;
        }
        public IBackgroundTaskQueue TaskQueue { get; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Queued Hosted Service is running.");

            await BackgroundProcessing(stoppingToken);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var workItem =
                    await TaskQueue.DequeueAsync(stoppingToken);

                try
                {
                    await workItem(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error occurred executing {WorkItem}.", nameof(workItem));
                }
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is stopping.");

            await base.StopAsync(stoppingToken);
        }
        //protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        //{
        //    while (!stoppingToken.IsCancellationRequested)
        //    {
        //        var workItem = await taskQueue.DequeueAsync(stoppingToken);

        //        try
        //        {
        //            if (workItem != null)
        //            {
        //                await workItem(stoppingToken);
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            logger.LogError(ex, "Error processing background Queue.");
        //        }
        //    }
        //}
    }
}
