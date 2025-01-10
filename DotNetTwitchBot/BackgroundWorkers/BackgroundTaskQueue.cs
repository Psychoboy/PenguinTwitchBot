
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace DotNetTwitchBot.BackgroundWorkers
{
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        //private readonly ConcurrentQueue<Func<CancellationToken, ValueTask>> _workItems = new();
        //private readonly SemaphoreSlim _signal = new(0);

        //public void QueueBackgroundWorkItem(Func<CancellationToken, ValueTask> workItem)
        //{
        //    if (workItem == null)
        //    {
        //        throw new ArgumentNullException(nameof(workItem));
        //    }
        //    _workItems.Enqueue(workItem);
        //    _signal.Release();
        //}

        //public async Task<Func<CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken)
        //{
        //    await _signal.WaitAsync(cancellationToken);
        //    _workItems.TryDequeue(out var workItem);

        //    return workItem!;
        //}

        private readonly Channel<Func<CancellationToken, ValueTask>> _queue;

        public BackgroundTaskQueue(int capacity)
        {
            // Capacity should be set based on the expected application load and
            // number of concurrent threads accessing the queue.            
            // BoundedChannelFullMode.Wait will cause calls to WriteAsync() to return a task,
            // which completes only when space became available. This leads to backpressure,
            // in case too many publishers/calls start accumulating.
            var options = new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            };
            _queue = Channel.CreateBounded<Func<CancellationToken, ValueTask>>(options);
        }

        public async ValueTask QueueBackgroundWorkItemAsync(
            Func<CancellationToken, ValueTask> workItem)
        {
            ArgumentNullException.ThrowIfNull(workItem);

            await _queue.Writer.WriteAsync(workItem);
        }

        public async ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(
            CancellationToken cancellationToken)
        {
            var workItem = await _queue.Reader.ReadAsync(cancellationToken);

            return workItem;
        }

    }
}
