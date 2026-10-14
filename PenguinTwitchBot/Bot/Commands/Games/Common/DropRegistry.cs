using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Commands.Games.Common
{
    public class DropRegistry(ILogger<DropRegistry> logger) : IDropRegistry
    {
        private readonly ConcurrentDictionary<string, PendingDropRecord> _pendingDrops = new(StringComparer.Ordinal);

        public void RegisterDrop(PendingDropRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            CleanupExpiredDrops();
            _pendingDrops[record.DropToken] = record;
        }

        public PendingDropRecord? GetPendingDrop(string dropToken)
        {
            if (string.IsNullOrWhiteSpace(dropToken)) return null;
            return _pendingDrops.TryGetValue(dropToken, out var record) ? record : null;
        }

        public IReadOnlyList<PendingDropRecord> GetActiveDrops(DropGameType? gameType = null)
        {
            var now = DateTime.UtcNow;
            return _pendingDrops.Values
                .Where(r => r.ExpiresAtUtc > now && (!gameType.HasValue || r.GameType == gameType.Value))
                .ToList();
        }

        public bool TryConsumeDrop(string dropToken, out PendingDropRecord? record, out string? failureReason)
        {
            record = null;
            failureReason = null;

            if (string.IsNullOrWhiteSpace(dropToken))
            {
                failureReason = "Empty drop token";
                return false;
            }

            if (!_pendingDrops.TryGetValue(dropToken, out var peekRecord))
            {
                failureReason = "Unknown or already-consumed drop token";
                logger.LogWarning("Rejected unknown or already-consumed dropToken completion request.");
                return false;
            }

            var now = DateTime.UtcNow;
            if (now > peekRecord.ExpiresAtUtc)
            {
                _pendingDrops.TryRemove(dropToken, out _);
                failureReason = $"Expired drop token for {peekRecord.GameType} user {peekRecord.Username}";
                logger.LogWarning("Rejected expired dropToken for {GameType} user {User}.", peekRecord.GameType, peekRecord.Username);
                return false;
            }

            if (now < peekRecord.EarliestCompletionUtc)
            {
                failureReason = $"Premature drop token completion for {peekRecord.GameType} user {peekRecord.Username}";
                logger.LogWarning("Rejected premature dropToken completion for {GameType} user {User} (arrived before minimum flight time).", peekRecord.GameType, peekRecord.Username);
                return false;
            }

            if (!_pendingDrops.TryRemove(dropToken, out record))
            {
                failureReason = "Drop token was already consumed concurrently";
                return false;
            }

            return true;
        }

        public void CleanupExpiredDrops()
        {
            var now = DateTime.UtcNow;
            foreach (var kvp in _pendingDrops)
            {
                if (kvp.Value.ExpiresAtUtc <= now)
                {
                    _pendingDrops.TryRemove(kvp.Key, out _);
                }
            }
        }
    }
}
