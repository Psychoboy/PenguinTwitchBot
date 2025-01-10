namespace DotNetTwitchBot.BackgroundWorkers
{
    public interface IBackgroundTaskQueue
    {
        //void QueueBackgroundWorkItem(Func<CancellationToken, ValueTask> workItem);
        //Task<Func<CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken);
        ValueTask QueueBackgroundWorkItemAsync(Func<CancellationToken, ValueTask> workItem);

        ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(
            CancellationToken cancellationToken);
    }
}
