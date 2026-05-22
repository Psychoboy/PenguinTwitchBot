namespace PenguinTwitchBot.Bot.Queues
{
    /// <summary>
    /// Process-wide semaphore that caps the total number of non-blocking queue actions
    /// executing concurrently across ALL queues, regardless of how many queues exist.
    /// Limit = ProcessorCount * 4, floored at 16, capped at 64.
    /// </summary>
    public sealed class GlobalConcurrencyLimiter
    {
        public static readonly int MaxConcurrency =
            Math.Clamp(Environment.ProcessorCount * 4, 16, 64);

        private readonly SemaphoreSlim _semaphore = new(MaxConcurrency, MaxConcurrency);

        public Task WaitAsync(CancellationToken cancellationToken) =>
            _semaphore.WaitAsync(cancellationToken);

        public void Release() => _semaphore.Release();

        public int CurrentCount => _semaphore.CurrentCount;
    }
}
