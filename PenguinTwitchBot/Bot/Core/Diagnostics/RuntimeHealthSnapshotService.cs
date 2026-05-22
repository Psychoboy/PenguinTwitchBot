using PenguinTwitchBot.Bot.Queues;
using System.Diagnostics;

namespace PenguinTwitchBot.Bot.Core.Diagnostics
{
    public class RuntimeHealthSnapshotService(
        ILogger<RuntimeHealthSnapshotService> logger,
        IQueueManager queueManager
    ) : BackgroundService
    {
        private const int LowAvailableWorkerThreadThreshold = 20;
        private const int HighPendingQueueActionsThreshold = 2000;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await timer.WaitForNextTickAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                try
                {
                    await LogSnapshotAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while logging runtime health snapshot.");
                }
            }
        }

        private async Task LogSnapshotAsync()
        {
            ThreadPool.GetAvailableThreads(out var availableWorkers, out var availableIocp);
            ThreadPool.GetMaxThreads(out var maxWorkers, out var maxIocp);
            ThreadPool.GetMinThreads(out var minWorkers, out var minIocp);

            var busyWorkers = maxWorkers - availableWorkers;
            var busyIocp = maxIocp - availableIocp;

            var managedMemoryBytes = GC.GetTotalMemory(false);
            var gcGen0 = GC.CollectionCount(0);
            var gcGen1 = GC.CollectionCount(1);
            var gcGen2 = GC.CollectionCount(2);

            using var process = Process.GetCurrentProcess();
            var workingSetMb = Math.Round(process.WorkingSet64 / 1024d / 1024d, 2);
            var privateMemoryMb = Math.Round(process.PrivateMemorySize64 / 1024d / 1024d, 2);
            var threadCount = process.Threads.Count;

            var queueSummary = "unavailable";
            var totalPendingActions = -1;
            try
            {
                var queueStats = await queueManager.GetAllQueueStatisticsAsync();
                var pending = queueStats.Sum(x => x.PendingActions);
                var executing = queueStats.Sum(x => x.CurrentlyExecuting);
                totalPendingActions = pending;
                queueSummary = $"count={queueStats.Count},pending={pending},executing={executing}";
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Queue statistics not available for health snapshot.");
            }

            if (availableWorkers <= LowAvailableWorkerThreadThreshold ||
                totalPendingActions >= HighPendingQueueActionsThreshold)
            {
                logger.LogWarning(
                    "[HEALTH-WARN] Potential saturation detected. availableWorkerThreads={AvailableWorkers}, busyWorkerThreads={BusyWorkers}/{MaxWorkers}, queuePendingActions={TotalPendingActions}, queueSummary={QueueSummary}, managedMemoryBytes={ManagedMemoryBytes}, processThreads={ThreadCount}",
                    availableWorkers,
                    busyWorkers,
                    maxWorkers,
                    totalPendingActions,
                    queueSummary,
                    managedMemoryBytes,
                    threadCount
                );
            }
        }
    }
}
