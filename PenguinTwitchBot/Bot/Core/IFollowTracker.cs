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
        event Action? SyncStateChanged;

        /// <summary>
        /// Checks whether a follow notification should be dispatched for this user and records
        /// a pending-dispatch follower entry if eligible. Returns <c>true</c> if follow events
        /// should fire (caller must then call <see cref="CompleteFollowDispatchAsync"/> on success
        /// or <see cref="ReleaseFollowDispatch"/> on failure), or <c>false</c> to suppress.
        /// </summary>
        Task<bool> TryBeginFollowDispatchAsync(
            string userId,
            string username,
            string displayName,
            DateTime followedAtUtc,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks the follower's notification as successfully dispatched and releases the in-flight lock.
        /// </summary>
        Task CompleteFollowDispatchAsync(
            string userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases the in-flight lock after a failed dispatch without marking the notification
        /// as completed, allowing a retry of the same follow event to complete dispatch.
        /// </summary>
        void ReleaseFollowDispatch(string userId);

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
