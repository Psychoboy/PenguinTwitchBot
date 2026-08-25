using System.Collections.Concurrent;

namespace PenguinTwitchBot.Helpers
{
    public sealed class KeyedSemaphore
    {
        private sealed class Entry
        {
            public readonly SemaphoreSlim Semaphore = new(1, 1);
            public int RefCount;
        }

        private readonly ConcurrentDictionary<string, Entry> entries;
        private readonly object gate = new();

        // Exposed for tests to verify entries are evicted once unused; not for production use.
        internal int TrackedKeyCount => entries.Count;

        public KeyedSemaphore(IEqualityComparer<string>? comparer = null)
        {
            entries = comparer == null ? new ConcurrentDictionary<string, Entry>() : new ConcurrentDictionary<string, Entry>(comparer);
        }

        public async Task<IDisposable> AcquireAsync(string key, CancellationToken cancellationToken)
        {
            Entry entry;
            lock (gate)
            {
                entry = entries.GetOrAdd(key, _ => new Entry());
                entry.RefCount++;
            }

            try
            {
                await entry.Semaphore.WaitAsync(cancellationToken);
            }
            catch
            {
                // Waiting failed (e.g. cancelled): undo the ref-count bump so the entry isn't
                // leaked forever, since Release() will never be called for this acquisition.
                lock (gate)
                {
                    entry.RefCount--;
                    if (entry.RefCount == 0)
                    {
                        entries.TryRemove(key, out _);
                    }
                }
                throw;
            }

            return new Releaser(this, key, entry);
        }

        private void Release(string key, Entry entry)
        {
            entry.Semaphore.Release();
            lock (gate)
            {
                entry.RefCount--;
                if (entry.RefCount == 0)
                {
                    entries.TryRemove(key, out _);
                }
            }
        }

        private sealed class Releaser(KeyedSemaphore owner, string key, Entry entry) : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                owner.Release(key, entry);
            }
        }
    }
}
