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
    }

    public class FishingOverlayAckService : IFishingOverlayAckService
    {
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<bool>> _pending = new();

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
            if (revealId == Guid.Empty) return false;
            if (_pending.TryRemove(revealId, out var tcs))
            {
                return tcs.TrySetResult(true);
            }
            return false;
        }
    }
}
