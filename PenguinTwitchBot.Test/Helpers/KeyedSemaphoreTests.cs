using PenguinTwitchBot.Helpers;
using Xunit;

namespace PenguinTwitchBot.Test.Helpers
{
    public class KeyedSemaphoreTests
    {
        [Fact]
        public async Task AcquireAsync_SameKey_SerializesConcurrentCallers()
        {
            var sut = new KeyedSemaphore();
            var concurrentCount = 0;
            var maxObservedConcurrency = 0;
            var gate = new object();

            async Task Work()
            {
                using var _ = await sut.AcquireAsync("shared-key", CancellationToken.None);
                lock (gate)
                {
                    concurrentCount++;
                    maxObservedConcurrency = Math.Max(maxObservedConcurrency, concurrentCount);
                }
                await Task.Delay(50);
                lock (gate)
                {
                    concurrentCount--;
                }
            }

            await Task.WhenAll(Work(), Work(), Work());

            Assert.Equal(1, maxObservedConcurrency);
        }

        [Fact]
        public async Task AcquireAsync_DifferentKeys_RunConcurrently()
        {
            var sut = new KeyedSemaphore();
            var bothEntered = new TaskCompletionSource();
            var entryCount = 0;

            async Task Work(string key)
            {
                using var _ = await sut.AcquireAsync(key, CancellationToken.None);
                if (Interlocked.Increment(ref entryCount) == 2)
                {
                    bothEntered.TrySetResult();
                }
                // If different keys serialized against each other, this would never complete
                // because the other task couldn't enter its critical section to signal.
                await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }

            await Task.WhenAll(Work("key-a"), Work("key-b"));

            Assert.Equal(2, entryCount);
        }

        [Fact]
        public async Task AcquireAsync_ReleasedLock_AllowsSubsequentAcquire()
        {
            var sut = new KeyedSemaphore();

            var first = await sut.AcquireAsync("key", CancellationToken.None);
            first.Dispose();

            var second = await sut.AcquireAsync("key", CancellationToken.None);
            second.Dispose();
        }

        [Fact]
        public async Task AcquireAsync_HonorsCustomComparer_ForCaseInsensitiveKeys()
        {
            var sut = new KeyedSemaphore(StringComparer.OrdinalIgnoreCase);

            using var lease = await sut.AcquireAsync("Command", CancellationToken.None);

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => sut.AcquireAsync("command", cts.Token));
        }

        [Fact]
        public async Task AcquireAsync_DistinctKeys_DoNotContendEvenWithCustomComparer()
        {
            var sut = new KeyedSemaphore(StringComparer.OrdinalIgnoreCase);

            using var lease = await sut.AcquireAsync("Command", CancellationToken.None);

            using var other = await sut.AcquireAsync("other-command", CancellationToken.None);
        }

        [Fact]
        public async Task AcquireAsync_CancelledBeforeLockAvailable_ThrowsAndReleasesRefCount()
        {
            var sut = new KeyedSemaphore();

            var holder = await sut.AcquireAsync("key", CancellationToken.None);
            Assert.Equal(1, sut.TrackedKeyCount);

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => sut.AcquireAsync("key", cts.Token));

            // The failed waiter must not leak a ref-count on the entry still held by "holder".
            Assert.Equal(1, sut.TrackedKeyCount);

            holder.Dispose();
            Assert.Equal(0, sut.TrackedKeyCount);
        }

        [Fact]
        public async Task AcquireAsync_AlreadyCancelledToken_ThrowsImmediatelyWithoutLeakingEntry()
        {
            var sut = new KeyedSemaphore();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => sut.AcquireAsync("key", cts.Token));

            Assert.Equal(0, sut.TrackedKeyCount);
        }

        [Fact]
        public async Task Dispose_RemovesEntryFromRegistry_WhenNoOtherHoldersOrWaiters()
        {
            var sut = new KeyedSemaphore();

            var lease = await sut.AcquireAsync("key", CancellationToken.None);
            Assert.Equal(1, sut.TrackedKeyCount);

            lease.Dispose();

            Assert.Equal(0, sut.TrackedKeyCount);
        }

        [Fact]
        public async Task Dispose_IsIdempotent_WhenCalledMultipleTimes()
        {
            var sut = new KeyedSemaphore();

            var lease = await sut.AcquireAsync("key", CancellationToken.None);
            lease.Dispose();
            lease.Dispose();

            // A second acquire must succeed immediately; a double-release would have
            // over-incremented the semaphore's available count and hidden a bug here.
            var second = await sut.AcquireAsync("key", CancellationToken.None);
            second.Dispose();
        }

        [Fact]
        public async Task Dispose_KeepsEntry_WhileOtherHoldersStillWaitingOrHolding()
        {
            var sut = new KeyedSemaphore();

            var first = await sut.AcquireAsync("key", CancellationToken.None);
            var secondTask = sut.AcquireAsync("key", CancellationToken.None);

            // Second acquire is still queued behind the first, so the entry must remain tracked.
            Assert.Equal(1, sut.TrackedKeyCount);

            first.Dispose();
            var second = await secondTask;

            Assert.Equal(1, sut.TrackedKeyCount);

            second.Dispose();
            Assert.Equal(0, sut.TrackedKeyCount);
        }

        [Fact]
        public async Task ManyDistinctKeys_AreAllEvicted_AfterRelease()
        {
            var sut = new KeyedSemaphore();

            var leases = new List<IDisposable>();
            for (var i = 0; i < 50; i++)
            {
                leases.Add(await sut.AcquireAsync($"key-{i}", CancellationToken.None));
            }

            Assert.Equal(50, sut.TrackedKeyCount);

            foreach (var lease in leases)
            {
                lease.Dispose();
            }

            Assert.Equal(0, sut.TrackedKeyCount);
        }
    }
}
