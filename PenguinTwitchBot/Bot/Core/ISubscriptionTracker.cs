namespace PenguinTwitchBot.Bot.Core
{
    public enum SubscriberFilter
    {
        Current,
        Past,
        All
    }

    public enum SubscriberLeaderboardMetric
    {
        TotalMonths,
        ConsecutiveMonths
    }

    public sealed record SubscriberSyncResult(
        int TotalTwitchSubscribers,
        int SubPoints,
        int AddedCount,
        int UpdatedCount,
        int MarkedPastCount,
        int ActiveCount,
        int PastCount,
        DateTime CompletedAtUtc);

    public sealed record SubscriberListItem(
        Guid Id,
        string UserId,
        string Username,
        string DisplayName,
        bool IsActive,
        string Tier,
        bool IsGift,
        string? GifterName,
        int TotalMonths,
        int ConsecutiveMonths,
        DateTime LastSub,
        DateTime? EndedAtUtc,
        Guid? ViewerId,
        DateTime? LastSeen);

    public sealed record SubscriberListPage(
        List<SubscriberListItem> Items,
        int TotalCount,
        int CurrentCount,
        int PastCount);

    public sealed record SubscriberLeaderboardEntry(
        int Rank,
        string UserId,
        string Username,
        string DisplayName,
        string Tier,
        int TotalMonths,
        int ConsecutiveMonths);

    public interface ISubscriptionTracker
    {
        bool IsSyncInProgress { get; }
        SubscriberSyncResult? LastSyncResult { get; }
        event Action? SyncStateChanged;

        Task<bool> ExistingSub(string name);
        Task<DateTime?> LastSub(string name);
        Task<SubscriptionHistory?> GetSubHistoryAsync(string username, string? userId = null, CancellationToken cancellationToken = default);
        Task<List<string>> MissingSubs(IEnumerable<string> names);
        Task AddOrUpdateSubHistory(string name, string userId);

        Task RecordSubscriptionAsync(
            string userId,
            string username,
            string displayName,
            string tier = "",
            bool? isGift = null,
            string? gifterName = null,
            bool isAnonymousGift = false,
            int? totalMonths = null,
            int? consecutiveMonths = null,
            bool isRenewal = false,
            CancellationToken cancellationToken = default);

        Task RecordChatSubscriberBadgeMonthsAsync(
            string userId,
            string username,
            string displayName,
            int badgeMonths,
            bool isActiveSubscriber = true,
            CancellationToken cancellationToken = default);

        Task RecordSubscriptionEndAsync(
            string userId,
            string username,
            CancellationToken cancellationToken = default);

        Task<SubscriberSyncResult> SyncSubscribersFromTwitchAsync(CancellationToken cancellationToken = default);

        Task<SubscriberListPage> GetSubscribersPageAsync(
            int page,
            int pageSize,
            SubscriberFilter filter = SubscriberFilter.Current,
            string? search = null,
            string? sortBy = null,
            bool sortDescending = true,
            CancellationToken cancellationToken = default);

        Task UpdateSubscriberMonthsAsync(
            Guid subscriptionHistoryId,
            int totalMonths,
            int consecutiveMonths,
            CancellationToken cancellationToken = default);

        Task<List<SubscriberLeaderboardEntry>> GetTopCurrentSubscribersAsync(
            SubscriberLeaderboardMetric metric,
            int limit = 10,
            CancellationToken cancellationToken = default);
    }
}
