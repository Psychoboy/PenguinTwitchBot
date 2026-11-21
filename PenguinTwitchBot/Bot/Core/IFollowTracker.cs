namespace PenguinTwitchBot.Bot.Core
{
    public sealed record FollowerSyncProgress(int FetchedCount, int TotalExpected);

    public sealed record FollowerSyncResult(
        int TotalTwitchFollowers,
        int AddedCount,
        int UpdatedCount,
        int ExistingCount,
        int TotalPersistedFollowers,
        DateTime CompletedAtUtc,
        int RemovedCount = 0,
        bool RemovedUnfollowed = false);

    public sealed record FollowerSyncScheduleConfig(
        bool Enabled = false,
        int IntervalDays = 30,
        bool RemoveUnfollowed = false,
        DateTime? LastScheduledRunUtc = null);

    public sealed record PersistedFollowerListItem(
        Guid FollowerId,
        string UserId,
        string Username,
        string DisplayName,
        DateTime FollowDate,
        Guid? ViewerId,
        DateTime? LastSeen,
        bool IsSub,
        bool IsVip,
        bool IsMod,
        bool IsBroadcaster);

    public sealed record PersistedFollowerPage(
        List<PersistedFollowerListItem> Items,
        int TotalCount);

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
        /// Fetches all current channel followers from Twitch and updates the persisted Followers table.
        /// When <paramref name="removeUnfollowed"/> is <c>false</c> (default), only adds/updates followers
        /// and never deletes previously persisted rows. When <c>true</c>, also removes followers who are
        /// no longer following on Twitch (excluding any live followers within the sync grace window).
        /// </summary>
        Task<FollowerSyncResult> SyncFollowersAsync(
            bool removeUnfollowed = false,
            IProgress<FollowerSyncProgress>? progress = null,
            CancellationToken cancellationToken = default);

        Task<int> GetPersistedFollowerCountAsync(CancellationToken cancellationToken = default);

        Task<bool> GetCheckLocalTableFirstAsync(CancellationToken cancellationToken = default);

        Task SetCheckLocalTableFirstAsync(bool enabled, CancellationToken cancellationToken = default);

        Task<FollowerSyncScheduleConfig> GetScheduleConfigAsync(CancellationToken cancellationToken = default);

        Task SaveScheduleConfigAsync(FollowerSyncScheduleConfig config, CancellationToken cancellationToken = default);

        Task<bool> TryRunScheduledSyncIfDueAsync(DateTime? nowUtc = null, CancellationToken cancellationToken = default);

        Task<Follower?> GetPersistedFollowerByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default);

        Task<PersistedFollowerPage> GetPersistedFollowersPageAsync(
            int page,
            int pageSize,
            string? search = null,
            string? sortBy = null,
            bool sortDescending = true,
            CancellationToken cancellationToken = default);
    }
}
