using Microsoft.AspNetCore.SignalR;
using PenguinTwitchBot.Bot.Hubs;

namespace PenguinTwitchBot.Bot.Overlay
{
    /// <summary>
    /// Holds the state of the on-stream timer overlay. The timer is never started automatically —
    /// it only runs once a sub-action (or the UI) explicitly starts it.
    /// </summary>
    public class StreamTimerService(IHubContext<MainHub> hubContext, ILogger<StreamTimerService> logger) : IStreamTimerService
    {
        public const string DirectionUp = "up";
        public const string DirectionDown = "down";

        private readonly Lock _lock = new();

        private bool _isRunning;
        private string _direction = DirectionUp;
        private double _anchorSeconds;
        private DateTime _anchorUtc = DateTime.UtcNow;

        public StreamTimerState GetState()
        {
            lock (_lock)
            {
                return BuildState();
            }
        }

        public Task StartAsync(string? direction = null, double? startSeconds = null, bool reset = false)
        {
            lock (_lock)
            {
                Anchor();

                if (!string.IsNullOrWhiteSpace(direction))
                {
                    _direction = direction.Equals(DirectionDown, StringComparison.OrdinalIgnoreCase)
                        ? DirectionDown
                        : DirectionUp;
                }

                if (startSeconds.HasValue)
                {
                    _anchorSeconds = Math.Max(0, startSeconds.Value);
                }
                else if (reset)
                {
                    _anchorSeconds = 0;
                }

                _isRunning = true;
            }

            return BroadcastAsync();
        }

        public Task StopAsync(bool reset = false)
        {
            lock (_lock)
            {
                Anchor();
                _isRunning = false;
                if (reset) _anchorSeconds = 0;
            }

            return BroadcastAsync();
        }

        public Task AddTimeAsync(double seconds) => AdjustAsync(seconds);

        public Task RemoveTimeAsync(double seconds) => AdjustAsync(-seconds);

        public Task SetTimeAsync(double seconds)
        {
            lock (_lock)
            {
                Anchor();
                _anchorSeconds = Math.Max(0, seconds);
            }

            return BroadcastAsync();
        }

        private Task AdjustAsync(double delta)
        {
            lock (_lock)
            {
                Anchor();
                _anchorSeconds = Math.Max(0, _anchorSeconds + delta);
            }

            return BroadcastAsync();
        }

        /// <summary>Collapses the elapsed running time into the anchor so mutations apply to the live value.</summary>
        private void Anchor()
        {
            _anchorSeconds = CurrentSeconds();
            _anchorUtc = DateTime.UtcNow;
        }

        private double CurrentSeconds()
        {
            if (!_isRunning) return _anchorSeconds;

            var elapsed = (DateTime.UtcNow - _anchorUtc).TotalSeconds;
            return _direction == DirectionDown
                ? Math.Max(0, _anchorSeconds - elapsed)
                : _anchorSeconds + elapsed;
        }

        private StreamTimerState BuildState()
            => new(_isRunning, _direction, _anchorSeconds, _anchorUtc, DateTime.UtcNow);

        private async Task BroadcastAsync()
        {
            var state = GetState();
            try
            {
                await hubContext.Clients.All.SendAsync("StreamTimerUpdate", state);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed broadcasting stream timer state");
            }
        }
    }
}
