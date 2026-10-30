using System.Collections.Concurrent;

namespace PenguinTwitchBot.Circuit
{
    public interface ISessionRevocationService
    {
        /// <summary>Raised with the user ID when that user's sessions are revoked.</summary>
        event Action<string>? SessionRevoked;

        void Revoke(string userId);

        /// <summary>True if the login at <paramref name="issuedAt"/> predates a revocation for the user.</summary>
        bool IsRevoked(string userId, DateTimeOffset? issuedAt);
    }

    // Revocations are in-memory only; a restart clears them.
    public class SessionRevocationService : ISessionRevocationService
    {
        private readonly ConcurrentDictionary<string, DateTimeOffset> _revokedAt = new();

        public event Action<string>? SessionRevoked;

        public void Revoke(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return;
            _revokedAt[userId] = DateTimeOffset.UtcNow;
            SessionRevoked?.Invoke(userId);
        }

        public bool IsRevoked(string userId, DateTimeOffset? issuedAt)
        {
            if (string.IsNullOrEmpty(userId) || !_revokedAt.TryGetValue(userId, out var revokedAt))
                return false;

            return issuedAt == null || issuedAt <= revokedAt;
        }
    }
}
