namespace PenguinTwitchBot.Bot.Core
{
    public sealed record FollowerSyncProgress(int FetchedCount, int TotalExpected);

    public sealed record FollowerSyncResult(
        int TotalTwitchFollowers,
        int AddedCount,
        int UpdatedCount,
        int ExistingCount,
        int TotalPersistedFollowers,
        DateTime CompletedAtUtc);

    public interface IFollowTracker
    {
        bool IsSyncInProgress { get; }
        FollowerSyncResult? LastSyncResult { get; }

        /// <summary>
        /// Records a channel follow if the user has not previously followed.
        /// Returns <c>true</c> if this is a first-time follow (events should fire),
        /// or <c>false</c> if the user has already followed previously (re-follow; suppress events).
        /// </summary>
        Task<bool> TryRecordFollowAsync(
            string userId,
            string username,
            string displayName,
            DateTime followedAtUtc,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Manually fetches all current channel followers from Twitch and persists any missing ones.
        /// Never deletes previously persisted followers so unfollowed viewers cannot re-trigger follow events.
        /// </summary>
        Task<FollowerSyncResult> SyncFollowersAsync(
            IProgress<FollowerSyncProgress>? progress = null,
            CancellationToken cancellationToken = default);

        Task<int> GetPersistedFollowerCountAsync(CancellationToken cancellationToken = default);
    }
}
