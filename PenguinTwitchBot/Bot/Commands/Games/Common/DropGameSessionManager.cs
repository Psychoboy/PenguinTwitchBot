using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PenguinTwitchBot.Bot.Commands.Games.Common
{
    public sealed class QueuedSessionRequest
    {
        public int? DurationSeconds { get; init; }
        public string? ThemeOverrideId { get; init; }
        public string? TriggeredBy { get; init; }
        public DateTime EnqueuedAtUtc { get; init; } = DateTime.UtcNow;
        public TaskCompletionSource<DropGameSessionDto>? CompletionSource { get; init; }
    }

    public sealed class DropGameSessionState
    {
        public DateTime StartedAtUtc { get; init; }
        public DateTime EndsAtUtc { get; set; }
        public int TotalDurationSeconds { get; set; }
        public string? ThemeIdOverride { get; set; }
        public string? StartedBy { get; init; }
        public ConcurrentDictionary<string, int> UserDropCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int TotalDrops;
        public double? TargetCenterX { get; set; }
        public double? TargetHalfWidth { get; set; }
        public double? BullseyeHalfWidth { get; set; }
        public CancellationTokenSource Cts { get; } = new();
        public Timer? ExpiryTimer { get; set; }
    }

    public sealed class DropGameSessionManager : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ConcurrentQueue<QueuedSessionRequest> _sessionQueue = new();
        private readonly SemaphoreSlim _sessionLock = new(1, 1);
        private DropGameSessionState? _activeSession;

        public DropGameSessionManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public SemaphoreSlim SessionLock => _sessionLock;

        public DropGameSessionState? ActiveSession => _activeSession;

        public ConcurrentQueue<QueuedSessionRequest> SessionQueue => _sessionQueue;

        public int QueuedCount => _sessionQueue.Count;

        public bool HasActiveTimedSession()
        {
            var state = _activeSession;
            return state != null && state.EndsAtUtc > DateTime.UtcNow;
        }

        public DropGameSessionDto? GetCurrentSessionDto(string gameType)
        {
            var state = _activeSession;
            if (state == null) return null;
            var now = DateTime.UtcNow;
            if (state.EndsAtUtc <= now) return null;

            var remainingSec = Math.Max(0, (int)Math.Ceiling((state.EndsAtUtc - now).TotalSeconds));
            return new DropGameSessionDto
            {
                GameType = gameType,
                IsActive = true,
                IsQueued = false,
                QueuePosition = 0,
                TotalDurationSeconds = state.TotalDurationSeconds,
                RemainingSeconds = remainingSec,
                StartedAtMs = new DateTimeOffset(state.StartedAtUtc).ToUnixTimeMilliseconds(),
                EndsAtMs = new DateTimeOffset(state.EndsAtUtc).ToUnixTimeMilliseconds(),
                TotalDrops = Volatile.Read(ref state.TotalDrops),
                TargetCenterX = state.TargetCenterX,
                TargetHalfWidth = state.TargetHalfWidth,
                BullseyeHalfWidth = state.BullseyeHalfWidth
            };
        }

        public QueuedSessionRequest EnqueueSession(int? durationSeconds, string? themeOverrideId, string? triggeredBy, TaskCompletionSource<DropGameSessionDto>? completionSource = null)
        {
            var item = new QueuedSessionRequest
            {
                DurationSeconds = durationSeconds,
                ThemeOverrideId = themeOverrideId,
                TriggeredBy = triggeredBy,
                EnqueuedAtUtc = DateTime.UtcNow,
                CompletionSource = completionSource
            };
            _sessionQueue.Enqueue(item);
            return item;
        }

        public int RemoveQueuedSessions()
        {
            var count = 0;
            while (_sessionQueue.TryDequeue(out var item))
            {
                count++;
                item.CompletionSource?.TrySetCanceled();
            }
            return count;
        }

        public bool TryDequeueNextQueuedSession(out QueuedSessionRequest? nextSession)
        {
            return _sessionQueue.TryDequeue(out nextSession);
        }

        public DropGameSessionState CreateAndSetActiveSession(
            int durationSeconds,
            string? themeOverrideId,
            string? triggeredBy,
            Func<DropGameSessionState, Task> onExpireCallback)
        {
            ArgumentNullException.ThrowIfNull(onExpireCallback);

            if (_activeSession != null)
            {
                _activeSession.ExpiryTimer?.Dispose();
                _activeSession.Cts.Cancel();
                _activeSession.Cts.Dispose();
                _activeSession = null;
            }

            var now = DateTime.UtcNow;
            var endsAt = now.AddSeconds(durationSeconds);
            var state = new DropGameSessionState
            {
                StartedAtUtc = now,
                EndsAtUtc = endsAt,
                TotalDurationSeconds = durationSeconds,
                ThemeIdOverride = themeOverrideId,
                StartedBy = triggeredBy
            };

            state.ExpiryTimer = new Timer(async _ =>
            {
                try
                {
                    await onExpireCallback(state);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error expiring session in DropGameSessionManager");
                }
            }, null, TimeSpan.FromSeconds(durationSeconds), Timeout.InfiniteTimeSpan);

            _activeSession = state;
            return state;
        }

        public DropGameSessionState? ExchangeStopSession(DropGameSessionState? expectedState = null)
        {
            DropGameSessionState? state;
            if (expectedState != null)
            {
                state = Interlocked.CompareExchange(ref _activeSession, null, expectedState) == expectedState ? expectedState : null;
            }
            else
            {
                state = Interlocked.Exchange(ref _activeSession, null);
            }

            if (state != null)
            {
                state.ExpiryTimer?.Dispose();
                state.Cts.Cancel();
                state.Cts.Dispose();
            }
            return state;
        }

        public bool CanUserDrop(string userId, int maxDropsPerUserPerSession)
        {
            return CanUserDrop(userId, maxDropsPerUserPerSession, out _, out _);
        }

        public bool CanUserDrop(string userId, int maxDropsPerUserPerSession, out bool isRoundEnded, out bool isMaxDropsReached)
        {
            isRoundEnded = false;
            isMaxDropsReached = false;
            var state = _activeSession;
            if (state == null) return true;

            if (state.EndsAtUtc <= DateTime.UtcNow)
            {
                isRoundEnded = true;
                return false;
            }

            if (maxDropsPerUserPerSession > 0)
            {
                if (state.UserDropCounts.TryGetValue(userId, out var count) && count >= maxDropsPerUserPerSession)
                {
                    isMaxDropsReached = true;
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Evaluates whether the active session is currently within its final shot clock / last-call cutoff window.
        /// Returns true if remaining session seconds are less than or equal to cutoffSeconds, protecting the round
        /// from late-arriving throws and preventing anti-stall buzzer griefing.
        /// </summary>
        public bool IsWithinLastCallCutoff(int cutoffSeconds, out double remainingSeconds)
        {
            remainingSeconds = 0.0;
            var state = _activeSession;
            if (state == null || cutoffSeconds <= 0) return false;

            remainingSeconds = (state.EndsAtUtc - DateTime.UtcNow).TotalSeconds;
            return remainingSeconds <= cutoffSeconds;
        }

        public void RecordUserDrop(string userId)
        {
            var state = _activeSession;
            if (state != null)
            {
                if (state.EndsAtUtc > DateTime.UtcNow)
                {
                    Interlocked.Increment(ref state.TotalDrops);
                    state.UserDropCounts.AddOrUpdate(userId, 1, (_, c) => c + 1);
                }
            }
        }

        public void Dispose()
        {
            RemoveQueuedSessions();
            var state = Interlocked.Exchange(ref _activeSession, null);
            if (state != null)
            {
                state.ExpiryTimer?.Dispose();
                state.Cts.Cancel();
                state.Cts.Dispose();
            }
            _sessionLock.Dispose();
        }
    }
}

