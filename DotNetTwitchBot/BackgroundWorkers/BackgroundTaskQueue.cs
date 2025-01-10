
using System.Collections.Concurrent;

namespace DotNetTwitchBot.BackgroundWorkers
{
    public class BackgroundTaskQueue(ILogger<BackgroundTaskQueue> logger) : IBackgroundTaskQueue
    {
        private readonly ConcurrentQueue<Func<CancellationToken, ValueTask>> _workItems = new();
        private readonly SemaphoreSlim _signal = new(0);

        public void QueueBackgroundWorkItem(Func<CancellationToken, ValueTask> workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }
            _workItems.Enqueue(workItem);
            _signal.Release();
        }

        public async Task<Func<CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken)
        {
            if (await _signal.WaitAsync(60000, cancellationToken) == false)
            { 
                logger.LogWarning("BackgroundTaskQueue timed out waiting for work item.");
            }
            _workItems.TryDequeue(out var workItem);

            return workItem!;
        }

    }
}
