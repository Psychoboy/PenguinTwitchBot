using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public interface IFishingOverlayAckService
    {
        /// <summary>Registers a pending reveal token before sending the WebSocket message to avoid any race.</summary>
        void Register(Guid revealId);

        /// <summary>Awaits until the fishing overlay POSTs /api/overlay/fishing-revealed or the timeout expires.</summary>
        Task<bool> WaitForRevealAsync(Guid revealId, TimeSpan timeout, CancellationToken cancellationToken = default);

        /// <summary>Signals that the fishing overlay has revealed the catch for <paramref name="revealId"/>.</summary>
        bool NotifyRevealed(Guid revealId);

        /// <summary>Removes an unawaited reveal registration so the pending table never leaks entries.</summary>
        void Unregister(Guid revealId);

        /// <summary>Records a heartbeat/ack from an overlay authenticated with the valid secure overlay key.</summary>
        void RecordRegisteredOverlay();

        /// <summary>Returns true if an overlay with the valid secure key has registered or acknowledged within <paramref name="maxAge"/>.</summary>
        bool HasActiveRegisteredOverlay(TimeSpan? maxAge = null);

        /// <summary>Clears the registered overlay timestamp (e.g., after a reveal wait timeout) so subsequent casts do not stall.</summary>
        void ClearRegisteredOverlay();
    }

    public class FishingOverlayAckService : IFishingOverlayAckService
    {
        private static readonly TimeSpan DefaultRegistrationTtl = TimeSpan.FromSeconds(90);

        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<bool>> _pending = new();
        private long _lastRegisteredOverlayUtcTicks;

        public void Register(Guid revealId)
        {
            if (revealId == Guid.Empty) return;
            _pending.TryAdd(revealId, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
        }

        public async Task<bool> WaitForRevealAsync(Guid revealId, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (revealId == Guid.Empty || !_pending.TryGetValue(revealId, out var tcs))
            {
                return false;
            }

            try
            {
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout, cancellationToken));
                return completed == tcs.Task && tcs.Task.IsCompletedSuccessfully && tcs.Task.Result;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                _pending.TryRemove(revealId, out _);
            }
        }

        public bool NotifyRevealed(Guid revealId)
        {
            RecordRegisteredOverlay();
            if (revealId == Guid.Empty) return false;
            if (_pending.TryRemove(revealId, out var tcs))
            {
                return tcs.TrySetResult(true);
            }
            return false;
        }

        public void Unregister(Guid revealId)
        {
            if (revealId == Guid.Empty) return;
            _pending.TryRemove(revealId, out _);
        }

        public void RecordRegisteredOverlay()
        {
            Interlocked.Exchange(ref _lastRegisteredOverlayUtcTicks, DateTime.UtcNow.Ticks);
        }

        public bool HasActiveRegisteredOverlay(TimeSpan? maxAge = null)
        {
            var lastTicks = Interlocked.Read(ref _lastRegisteredOverlayUtcTicks);
            if (lastTicks <= 0) return false;
            var ttl = maxAge ?? DefaultRegistrationTtl;
            return (DateTime.UtcNow - new DateTime(lastTicks, DateTimeKind.Utc)) <= ttl;
        }

        public void ClearRegisteredOverlay()
        {
            Interlocked.Exchange(ref _lastRegisteredOverlayUtcTicks, 0);
        }
    }
}
