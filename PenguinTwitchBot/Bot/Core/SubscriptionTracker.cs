using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Bot.Core;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Core
{
    public class SubscriptionTracker(
        ILogger<SubscriptionTracker> logger,
        IServiceScopeFactory scopeFactory,
        ITwitchService? twitchService = null,
        IServiceBackbone? serviceBackbone = null) : ISubscriptionTracker
    {
        private readonly SemaphoreSlim _recordLock = new(1, 1);
        private readonly SemaphoreSlim _syncLock = new(1, 1);
        private readonly ConcurrentDictionary<string, int> _observedChatBadgeMonths = new(StringComparer.Ordinal);
        private volatile bool _isSyncInProgress;
        private SubscriberSyncResult? _lastSyncResult;

        public bool IsSyncInProgress => _isSyncInProgress;
        public SubscriberSyncResult? LastSyncResult => _lastSyncResult;
        public event Action? SyncStateChanged;

        public async Task<bool> ExistingSub(string name)
        {
            try
            {
                var normalized = UsernameNormalizer.Normalize(name);
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                return await db.SubscriptionHistories.Find(x => x.Username.Equals(normalized)).AnyAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking existing sub");
                return false;
            }
        }

        public async Task<DateTime?> LastSub(string name)
        {
            try
            {
                var normalized = UsernameNormalizer.Normalize(name);
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var lastSub = await db.SubscriptionHistories.Find(x => x.Username.Equals(normalized)).FirstOrDefaultAsync();
                if (lastSub != null)
                {
                    return lastSub.LastSub;
                }
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking existing sub");
                return null;
            }
        }

        public async Task<SubscriptionHistory?> GetSubHistoryAsync(
            string username,
            string? userId = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var normalized = UsernameNormalizer.Normalize(username);
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                if (!string.IsNullOrWhiteSpace(userId))
                {
                    var byId = await db.SubscriptionHistories
                        .Find(x => x.UserId == userId)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(cancellationToken);
                    if (byId != null)
                    {
                        return byId;
                    }
                }

                if (string.IsNullOrWhiteSpace(normalized))
                {
                    return null;
                }

                return await db.SubscriptionHistories
                    .Find(x => x.Username == normalized)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving sub history for {Username}", username);
                return null;
            }
        }

        public async Task<List<string>> MissingSubs(IEnumerable<string> names)
        {
            try
            {
                var normalizedNames = names.Select(UsernameNormalizer.Normalize).ToList();
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var foundNames = await db.SubscriptionHistories.Find(x => normalizedNames.Contains(x.Username)).Select(x => x.Username).ToListAsync();
                var missingNames = normalizedNames.Where(x => foundNames.Contains(x) == false).ToList();
                return missingNames;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking missing subs");
                return [];
            }
        }

        public async Task AddOrUpdateSubHistory(string name, string userId)
        {
            try
            {
                var normalized = UsernameNormalizer.Normalize(name);
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var subHistory = await db.SubscriptionHistories.Find(x => x.Username.Equals(normalized)).FirstOrDefaultAsync();
                subHistory ??= new SubscriptionHistory()
                {
                    UserId = userId
                };
                subHistory.Username = normalized;
                subHistory.LastSub = DateTime.UtcNow;
                subHistory.UpdatedAtUtc = DateTime.UtcNow;
                db.SubscriptionHistories.Update(subHistory);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error Adding or Updating sub history");
            }
        }

        public async Task RecordSubscriptionAsync(
            string userId,
            string username,
            string displayName,
            string tier = "",
            bool isGift = false,
            string? gifterName = null,
            int? totalMonths = null,
            int? consecutiveMonths = null,
            bool isRenewal = false,
            CancellationToken cancellationToken = default)
        {
            var normalizedUsername = UsernameNormalizer.Normalize(username);
            if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(normalizedUsername))
            {
                return;
            }
            if (IsExcludedUsername(normalizedUsername))
            {
                return;
            }

            await _recordLock.WaitAsync(cancellationToken);
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var existing = await FindExistingAsync(db, userId, normalizedUsername, cancellationToken);
                var now = DateTime.UtcNow;

                var isNew = existing == null;
                existing ??= new SubscriptionHistory
                {
                    UserId = userId ?? string.Empty,
                    Username = normalizedUsername
                };

                if (!string.IsNullOrWhiteSpace(userId))
                {
                    existing.UserId = userId;
                }
                if (!string.IsNullOrWhiteSpace(normalizedUsername))
                {
                    existing.Username = normalizedUsername;
                }
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    existing.DisplayName = displayName;
                }
                if (!string.IsNullOrWhiteSpace(tier))
                {
                    existing.Tier = tier;
                }

                existing.IsActive = true;
                existing.IsGift = isGift;
                if (!string.IsNullOrWhiteSpace(gifterName))
                {
                    existing.GifterName = gifterName;
                }
                else if (!isGift)
                {
                    existing.GifterName = null;
                }

                existing.LastSub = now;
                existing.EndedAtUtc = null;
                existing.UpdatedAtUtc = now;

                if (totalMonths.HasValue && totalMonths.Value > 0)
                {
                    existing.TotalMonths = Math.Max(existing.TotalMonths, totalMonths.Value);
                }
                else if (!isRenewal && existing.TotalMonths == 0)
                {
                    existing.TotalMonths = 1;
                }

                if (consecutiveMonths.HasValue && consecutiveMonths.Value > 0)
                {
                    existing.ConsecutiveMonths = consecutiveMonths.Value;
                    if (existing.TotalMonths < existing.ConsecutiveMonths)
                    {
                        existing.TotalMonths = existing.ConsecutiveMonths;
                    }
                }
                else if (!isRenewal && existing.ConsecutiveMonths == 0)
                {
                    existing.ConsecutiveMonths = 1;
                }

                if (isNew)
                {
                    await db.SubscriptionHistories.AddAsync(existing);
                }
                else
                {
                    db.SubscriptionHistories.Update(existing);
                }

                await db.SaveChangesAsync();

                if (!string.IsNullOrWhiteSpace(existing.UserId) && existing.TotalMonths > 0)
                {
                    _observedChatBadgeMonths.AddOrUpdate(
                        existing.UserId,
                        existing.TotalMonths,
                        (_, prev) => Math.Max(prev, existing.TotalMonths));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error recording subscription for {Username} ({UserId})", username, userId);
            }
            finally
            {
                _recordLock.Release();
            }
        }

        public async Task RecordChatSubscriberBadgeMonthsAsync(
            string userId,
            string username,
            string displayName,
            int badgeMonths,
            CancellationToken cancellationToken = default)
        {
            if (badgeMonths <= 0 || string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            if (_observedChatBadgeMonths.TryGetValue(userId, out var cachedMonths) && badgeMonths <= cachedMonths)
            {
                return;
            }

            var normalizedUsername = UsernameNormalizer.Normalize(username);
            if (IsExcludedUsername(normalizedUsername))
            {
                return;
            }

            await _recordLock.WaitAsync(cancellationToken);
            try
            {
                if (_observedChatBadgeMonths.TryGetValue(userId, out cachedMonths) && badgeMonths <= cachedMonths)
                {
                    return;
                }

                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var existing = await FindExistingAsync(db, userId, normalizedUsername, cancellationToken);
                var now = DateTime.UtcNow;

                if (existing == null)
                {
                    var created = new SubscriptionHistory
                    {
                        UserId = userId,
                        Username = normalizedUsername,
                        DisplayName = displayName ?? string.Empty,
                        IsActive = true,
                        TotalMonths = badgeMonths,
                        LastSub = now,
                        UpdatedAtUtc = now
                    };
                    await db.SubscriptionHistories.AddAsync(created);
                    await db.SaveChangesAsync();
                    _observedChatBadgeMonths[userId] = badgeMonths;
                    return;
                }

                var changed = false;
                if (string.IsNullOrWhiteSpace(existing.UserId))
                {
                    existing.UserId = userId;
                    changed = true;
                }
                if (!string.IsNullOrWhiteSpace(normalizedUsername) && existing.Username != normalizedUsername)
                {
                    existing.Username = normalizedUsername;
                    changed = true;
                }
                if (!string.IsNullOrWhiteSpace(displayName) && existing.DisplayName != displayName)
                {
                    existing.DisplayName = displayName;
                    changed = true;
                }
                if (!existing.IsActive)
                {
                    existing.IsActive = true;
                    existing.EndedAtUtc = null;
                    changed = true;
                }
                if (badgeMonths > existing.TotalMonths)
                {
                    existing.TotalMonths = badgeMonths;
                    changed = true;
                }

                if (changed)
                {
                    existing.UpdatedAtUtc = now;
                    db.SubscriptionHistories.Update(existing);
                    await db.SaveChangesAsync();
                }

                _observedChatBadgeMonths[userId] = Math.Max(existing.TotalMonths, badgeMonths);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error recording subscriber badge months for {Username} ({UserId})", username, userId);
            }
            finally
            {
                _recordLock.Release();
            }
        }

        public async Task RecordSubscriptionEndAsync(
            string userId,
            string username,
            CancellationToken cancellationToken = default)
        {
            var normalizedUsername = UsernameNormalizer.Normalize(username);
            if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(normalizedUsername))
            {
                return;
            }
            if (IsExcludedUsername(normalizedUsername))
            {
                return;
            }

            await _recordLock.WaitAsync(cancellationToken);
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var existing = await FindExistingAsync(db, userId, normalizedUsername, cancellationToken);
                if (existing == null)
                {
                    return;
                }

                var now = DateTime.UtcNow;
                existing.IsActive = false;
                existing.ConsecutiveMonths = 0;
                existing.EndedAtUtc = now;
                existing.UpdatedAtUtc = now;
                db.SubscriptionHistories.Update(existing);
                await db.SaveChangesAsync();

                if (!string.IsNullOrWhiteSpace(existing.UserId))
                {
                    _observedChatBadgeMonths.TryRemove(existing.UserId, out _);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error recording subscription end for {Username} ({UserId})", username, userId);
            }
            finally
            {
                _recordLock.Release();
            }
        }

        public async Task<SubscriberSyncResult> SyncSubscribersFromTwitchAsync(CancellationToken cancellationToken = default)
        {
            if (twitchService == null)
            {
                throw new InvalidOperationException("TwitchService is not configured for subscriber sync.");
            }

            if (!_syncLock.Wait(0))
            {
                throw new InvalidOperationException("A subscriber sync is already in progress.");
            }

            _isSyncInProgress = true;
            NotifySyncStateChanged();
            try
            {
                logger.LogInformation("Starting subscriber sync from Twitch.");
                var snapshot = await twitchService.GetBroadcasterSubscriptionsSnapshotAsync(cancellationToken);
                var excludedUserIds = await GetExcludedUserIdsAsync();
                var excludedUsernames = GetExcludedUsernames();

                var deduplicated = snapshot.Subscriptions
                    .Where(s => !string.IsNullOrWhiteSpace(s.UserId))
                    .Where(s => !IsExcludedAccount(s.UserId, s.UserLogin, excludedUserIds, excludedUsernames))
                    .DistinctBy(s => s.UserId, StringComparer.Ordinal)
                    .ToList();

                var addedCount = 0;
                var updatedCount = 0;
                var markedPastCount = 0;
                var now = DateTime.UtcNow;

                await _recordLock.WaitAsync(cancellationToken);
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                    var allHistories = await db.SubscriptionHistories.Find(_ => true).ToListAsync(cancellationToken);
                    var byUserId = new Dictionary<string, SubscriptionHistory>(StringComparer.Ordinal);
                    var byUsername = new Dictionary<string, SubscriptionHistory>(StringComparer.Ordinal);
                    var duplicateRows = new List<SubscriptionHistory>();

                    foreach (var row in allHistories)
                    {
                        var normalizedRowUser = UsernameNormalizer.Normalize(row.Username);
                        if (IsExcludedAccount(row.UserId, normalizedRowUser, excludedUserIds, excludedUsernames))
                        {
                            db.SubscriptionHistories.Remove(row);
                            duplicateRows.Add(row);
                            continue;
                        }

                        SubscriptionHistory? canonical = null;

                        if (!string.IsNullOrWhiteSpace(row.UserId) && byUserId.TryGetValue(row.UserId, out var existingById))
                        {
                            canonical = existingById;
                        }
                        else if (!string.IsNullOrWhiteSpace(normalizedRowUser) && byUsername.TryGetValue(normalizedRowUser, out var existingByName))
                        {
                            canonical = existingByName;
                        }

                        if (canonical != null)
                        {
                            // Merge best historical metadata into canonical row and remove duplicate
                            var merged = false;
                            if (string.IsNullOrWhiteSpace(canonical.UserId) && !string.IsNullOrWhiteSpace(row.UserId))
                            {
                                canonical.UserId = row.UserId;
                                byUserId[row.UserId] = canonical;
                                merged = true;
                            }
                            if (string.IsNullOrWhiteSpace(canonical.DisplayName) && !string.IsNullOrWhiteSpace(row.DisplayName))
                            {
                                canonical.DisplayName = row.DisplayName;
                                merged = true;
                            }
                            if (row.TotalMonths > canonical.TotalMonths)
                            {
                                canonical.TotalMonths = row.TotalMonths;
                                merged = true;
                            }
                            if (row.ConsecutiveMonths > canonical.ConsecutiveMonths)
                            {
                                canonical.ConsecutiveMonths = row.ConsecutiveMonths;
                                merged = true;
                            }
                            if (row.LastSub > canonical.LastSub)
                            {
                                canonical.LastSub = row.LastSub;
                                merged = true;
                            }
                            if (merged)
                            {
                                canonical.UpdatedAtUtc = now;
                                db.SubscriptionHistories.Update(canonical);
                            }

                            db.SubscriptionHistories.Remove(row);
                            duplicateRows.Add(row);
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(row.UserId))
                        {
                            byUserId[row.UserId] = row;
                        }
                        if (!string.IsNullOrWhiteSpace(normalizedRowUser))
                        {
                            byUsername[normalizedRowUser] = row;
                        }
                    }

                    if (duplicateRows.Count > 0)
                    {
                        var duplicateSet = new HashSet<SubscriptionHistory>(duplicateRows);
                        allHistories.RemoveAll(duplicateSet.Contains);
                    }

                    var allViewers = await db.Viewers.Find(_ => true).ToListAsync(cancellationToken);
                    var viewersByUserId = new Dictionary<string, Viewer>(StringComparer.Ordinal);
                    foreach (var v in allViewers)
                    {
                        if (!string.IsNullOrWhiteSpace(v.UserId))
                        {
                            viewersByUserId.TryAdd(v.UserId, v);
                        }
                    }

                    var activeTwitchUserIds = new HashSet<string>(StringComparer.Ordinal);
                    var activeHistoryRows = new HashSet<SubscriptionHistory>();

                    foreach (var sub in deduplicated)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        activeTwitchUserIds.Add(sub.UserId);
                        var normalizedUsername = UsernameNormalizer.Normalize(sub.UserLogin);
                        var displayName = sub.UserName ?? string.Empty;

                        if (!byUserId.TryGetValue(sub.UserId, out var history))
                        {
                            byUsername.TryGetValue(normalizedUsername, out history);
                        }

                        if (history == null)
                        {
                            history = new SubscriptionHistory
                            {
                                UserId = sub.UserId,
                                Username = normalizedUsername,
                                DisplayName = displayName,
                                IsActive = true,
                                Tier = sub.Tier ?? string.Empty,
                                IsGift = sub.IsGift,
                                GifterName = sub.IsGift ? (sub.GifterName ?? sub.GifterLogin) : null,
                                LastSub = now,
                                EndedAtUtc = null,
                                UpdatedAtUtc = now
                            };
                            await db.SubscriptionHistories.AddAsync(history);
                            byUserId[sub.UserId] = history;
                            byUsername[normalizedUsername] = history;
                            addedCount++;
                        }
                        else
                        {
                            var gifter = sub.IsGift ? (sub.GifterName ?? sub.GifterLogin) : null;
                            var changed = false;

                            if (history.UserId != sub.UserId) { history.UserId = sub.UserId; changed = true; }
                            if (history.Username != normalizedUsername) { history.Username = normalizedUsername; changed = true; }
                            if (!string.IsNullOrWhiteSpace(displayName) && history.DisplayName != displayName) { history.DisplayName = displayName; changed = true; }
                            if (!history.IsActive) { history.IsActive = true; history.EndedAtUtc = null; changed = true; }
                            if (!string.IsNullOrWhiteSpace(sub.Tier) && history.Tier != sub.Tier) { history.Tier = sub.Tier; changed = true; }
                            if (history.IsGift != sub.IsGift) { history.IsGift = sub.IsGift; changed = true; }
                            if (history.GifterName != gifter) { history.GifterName = gifter; changed = true; }

                            if (changed)
                            {
                                history.UpdatedAtUtc = now;
                                db.SubscriptionHistories.Update(history);
                                updatedCount++;
                            }

                            byUserId[sub.UserId] = history;
                        }

                        activeHistoryRows.Add(history);

                        // Keep Viewers.isSub in sync
                        if (!viewersByUserId.TryGetValue(sub.UserId, out var viewer))
                        {
                            viewer = new Viewer
                            {
                                UserId = sub.UserId,
                                Username = normalizedUsername,
                                DisplayName = displayName,
                                isSub = true
                            };
                            await db.Viewers.AddAsync(viewer);
                            viewersByUserId[sub.UserId] = viewer;
                        }
                        else
                        {
                            var viewerChanged = false;
                            if (!viewer.isSub) { viewer.isSub = true; viewerChanged = true; }
                            if (viewer.Username != normalizedUsername) { viewer.Username = normalizedUsername; viewerChanged = true; }
                            if (!string.IsNullOrWhiteSpace(displayName) && viewer.DisplayName != displayName) { viewer.DisplayName = displayName; viewerChanged = true; }
                            if (viewerChanged)
                            {
                                db.Viewers.Update(viewer);
                            }
                        }
                    }

                    // Mark previously active SubscriptionHistories no longer in Twitch's list as Past
                    foreach (var row in allHistories)
                    {
                        if (!row.IsActive)
                        {
                            continue;
                        }

                        if (!activeHistoryRows.Contains(row))
                        {
                            row.IsActive = false;
                            row.ConsecutiveMonths = 0;
                            row.EndedAtUtc = now;
                            row.UpdatedAtUtc = now;
                            db.SubscriptionHistories.Update(row);
                            markedPastCount++;
                            if (!string.IsNullOrWhiteSpace(row.UserId))
                            {
                                _observedChatBadgeMonths.TryRemove(row.UserId, out _);
                            }
                        }
                    }

                    // Clear Viewers.isSub for viewers no longer subscribed
                    foreach (var viewer in allViewers)
                    {
                        if (viewer.isSub && !activeTwitchUserIds.Contains(viewer.UserId))
                        {
                            viewer.isSub = false;
                            db.Viewers.Update(viewer);
                        }
                    }

                    await db.SaveChangesAsync();

                    var activeCount = await db.SubscriptionHistories.Find(s => s.IsActive).CountAsync(cancellationToken);
                    var pastCount = await db.SubscriptionHistories.Find(s => !s.IsActive).CountAsync(cancellationToken);

                    var result = new SubscriberSyncResult(
                        TotalTwitchSubscribers: deduplicated.Count,
                        SubPoints: snapshot.Points,
                        AddedCount: addedCount,
                        UpdatedCount: updatedCount,
                        MarkedPastCount: markedPastCount,
                        ActiveCount: activeCount,
                        PastCount: pastCount,
                        CompletedAtUtc: DateTime.UtcNow);

                    _lastSyncResult = result;
                    logger.LogInformation(
                        "Subscriber sync completed: {TotalTwitch} from Twitch ({Points} pts), {Added} added, {Updated} updated, {MarkedPast} marked past ({Active} active, {Past} past).",
                        result.TotalTwitchSubscribers,
                        result.SubPoints,
                        result.AddedCount,
                        result.UpdatedCount,
                        result.MarkedPastCount,
                        result.ActiveCount,
                        result.PastCount);

                    return result;
                }
                finally
                {
                    _recordLock.Release();
                }
            }
            finally
            {
                _isSyncInProgress = false;
                _syncLock.Release();
                NotifySyncStateChanged();
            }
        }

        public async Task<SubscriberListPage> GetSubscribersPageAsync(
            int page,
            int pageSize,
            SubscriberFilter filter = SubscriberFilter.Current,
            string? search = null,
            string? sortBy = null,
            bool sortDescending = true,
            CancellationToken cancellationToken = default)
        {
            var safePage = Math.Max(0, page);
            var safePageSize = Math.Clamp(pageSize, 1, 100);

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var excludedUsernames = GetExcludedUsernames();

            var baseQuery = db.SubscriptionHistories.Find(_ => true).AsNoTracking();
            if (excludedUsernames.Count > 0)
            {
                baseQuery = baseQuery.Where(s => !excludedUsernames.Contains(s.Username));
            }

            var currentCount = await baseQuery.Where(s => s.IsActive).CountAsync(cancellationToken);
            var pastCount = await baseQuery.Where(s => !s.IsActive).CountAsync(cancellationToken);

            var query = filter switch
            {
                SubscriberFilter.Current => baseQuery.Where(s => s.IsActive),
                SubscriberFilter.Past => baseQuery.Where(s => !s.IsActive),
                _ => baseQuery
            };

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = UsernameNormalizer.Normalize(search);
                query = query.Where(s =>
                    s.Username.Contains(normalizedSearch) ||
                    s.DisplayName.ToLower().Contains(normalizedSearch));
            }

            var totalCount = await query.CountAsync(cancellationToken);

            query = (sortBy?.ToLowerInvariant(), sortDescending) switch
            {
                ("username", false) => query.OrderBy(s => s.Username),
                ("username", true) => query.OrderByDescending(s => s.Username),
                ("tier", false) => query.OrderBy(s => s.Tier).ThenByDescending(s => s.TotalMonths).ThenBy(s => s.Username),
                ("tier", true) => query.OrderByDescending(s => s.Tier).ThenByDescending(s => s.TotalMonths).ThenBy(s => s.Username),
                ("consecutivemonths", false) => query.OrderBy(s => s.ConsecutiveMonths).ThenBy(s => s.TotalMonths).ThenBy(s => s.Username),
                ("consecutivemonths", true) => query.OrderByDescending(s => s.ConsecutiveMonths).ThenByDescending(s => s.TotalMonths).ThenBy(s => s.Username),
                ("lastsub", false) => query.OrderBy(s => s.LastSub).ThenBy(s => s.Username),
                ("lastsub", true) => query.OrderByDescending(s => s.LastSub).ThenBy(s => s.Username),
                ("totalmonths", false) => query.OrderBy(s => s.TotalMonths).ThenBy(s => s.ConsecutiveMonths).ThenBy(s => s.Username),
                _ => query.OrderByDescending(s => s.TotalMonths).ThenByDescending(s => s.ConsecutiveMonths).ThenBy(s => s.Username)
            };

            var rows = await query
                .Skip(safePage * safePageSize)
                .Take(safePageSize)
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                return new SubscriberListPage([], totalCount, currentCount, pastCount);
            }

            var userIds = rows.Where(r => !string.IsNullOrWhiteSpace(r.UserId)).Select(r => r.UserId).Distinct().ToList();
            var usernames = rows.Where(r => string.IsNullOrWhiteSpace(r.UserId)).Select(r => r.Username).Distinct().ToList();

            var viewersByUserId = userIds.Count > 0
                ? await db.Viewers.Find(v => userIds.Contains(v.UserId)).AsNoTracking().ToDictionaryAsync(v => v.UserId, StringComparer.Ordinal, cancellationToken)
                : new Dictionary<string, Viewer>(StringComparer.Ordinal);

            var viewersByUsername = usernames.Count > 0
                ? await db.Viewers.Find(v => usernames.Contains(v.Username)).AsNoTracking().ToDictionaryAsync(v => v.Username, StringComparer.Ordinal, cancellationToken)
                : new Dictionary<string, Viewer>(StringComparer.Ordinal);

            var items = rows.Select(s =>
            {
                Viewer? viewer = null;
                if (!string.IsNullOrWhiteSpace(s.UserId))
                {
                    viewersByUserId.TryGetValue(s.UserId, out viewer);
                }
                if (viewer == null && !string.IsNullOrWhiteSpace(s.Username))
                {
                    viewersByUsername.TryGetValue(s.Username, out viewer);
                }

                DateTime? lastSeen = viewer != null && viewer.LastSeen != DateTime.MinValue
                    ? viewer.LastSeen
                    : null;

                return new SubscriberListItem(
                    Id: s.Id,
                    UserId: s.UserId,
                    Username: s.Username,
                    DisplayName: !string.IsNullOrWhiteSpace(s.DisplayName) ? s.DisplayName : (viewer?.DisplayName ?? s.Username),
                    IsActive: s.IsActive,
                    Tier: s.Tier,
                    IsGift: s.IsGift,
                    GifterName: s.GifterName,
                    TotalMonths: s.TotalMonths,
                    ConsecutiveMonths: s.ConsecutiveMonths,
                    LastSub: s.LastSub,
                    EndedAtUtc: s.EndedAtUtc,
                    ViewerId: viewer?.Id,
                    LastSeen: lastSeen);
            }).ToList();

            return new SubscriberListPage(items, totalCount, currentCount, pastCount);
        }

        public async Task UpdateSubscriberMonthsAsync(
            Guid subscriptionHistoryId,
            int totalMonths,
            int consecutiveMonths,
            CancellationToken cancellationToken = default)
        {
            if (totalMonths < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalMonths), "Total months cannot be negative.");
            }
            if (consecutiveMonths < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(consecutiveMonths), "Consecutive months cannot be negative.");
            }

            var safeTotal = Math.Max(totalMonths, consecutiveMonths);

            await _recordLock.WaitAsync(cancellationToken);
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var existing = await db.SubscriptionHistories
                    .Find(s => s.Id == subscriptionHistoryId)
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException("Subscriber record not found.");

                existing.TotalMonths = safeTotal;
                existing.ConsecutiveMonths = consecutiveMonths;
                existing.UpdatedAtUtc = DateTime.UtcNow;

                db.SubscriptionHistories.Update(existing);
                await db.SaveChangesAsync();

                if (!string.IsNullOrWhiteSpace(existing.UserId))
                {
                    if (safeTotal > 0)
                    {
                        _observedChatBadgeMonths[existing.UserId] = safeTotal;
                    }
                    else
                    {
                        _observedChatBadgeMonths.TryRemove(existing.UserId, out _);
                    }
                }
            }
            finally
            {
                _recordLock.Release();
            }
        }

        public async Task<List<SubscriberLeaderboardEntry>> GetTopCurrentSubscribersAsync(
            SubscriberLeaderboardMetric metric,
            int limit = 10,
            CancellationToken cancellationToken = default)
        {
            var safeLimit = Math.Clamp(limit, 1, 100);

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var excludedUsernames = GetExcludedUsernames();

            var query = db.SubscriptionHistories
                .Find(s => s.IsActive)
                .AsNoTracking();

            if (excludedUsernames.Count > 0)
            {
                query = query.Where(s => !excludedUsernames.Contains(s.Username));
            }

            query = metric == SubscriberLeaderboardMetric.ConsecutiveMonths
                ? query
                    .Where(s => s.ConsecutiveMonths > 0)
                    .OrderByDescending(s => s.ConsecutiveMonths)
                    .ThenByDescending(s => s.TotalMonths)
                    .ThenBy(s => s.LastSub)
                    .ThenBy(s => s.Username)
                : query
                    .Where(s => s.TotalMonths > 0)
                    .OrderByDescending(s => s.TotalMonths)
                    .ThenByDescending(s => s.ConsecutiveMonths)
                    .ThenBy(s => s.LastSub)
                    .ThenBy(s => s.Username);

            var rows = await query
                .Take(safeLimit)
                .ToListAsync(cancellationToken);

            var result = new List<SubscriberLeaderboardEntry>(rows.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                result.Add(new SubscriberLeaderboardEntry(
                    Rank: i + 1,
                    UserId: row.UserId,
                    Username: row.Username,
                    DisplayName: !string.IsNullOrWhiteSpace(row.DisplayName) ? row.DisplayName : row.Username,
                    Tier: row.Tier,
                    TotalMonths: row.TotalMonths,
                    ConsecutiveMonths: row.ConsecutiveMonths));
            }

            return result;
        }

        private static async Task<SubscriptionHistory?> FindExistingAsync(
            IUnitOfWork db,
            string? userId,
            string normalizedUsername,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var byUserId = await db.SubscriptionHistories
                    .Find(x => x.UserId == userId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (byUserId != null)
                {
                    return byUserId;
                }
            }

            if (!string.IsNullOrWhiteSpace(normalizedUsername))
            {
                return await db.SubscriptionHistories
                    .Find(x => x.Username == normalizedUsername)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            return null;
        }

        private void NotifySyncStateChanged()
        {
            try
            {
                SyncStateChanged?.Invoke();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error notifying subscriber SyncStateChanged listeners.");
            }
        }

        private List<string> GetExcludedUsernames()
        {
            var excluded = new List<string>(2);
            if (serviceBackbone == null)
            {
                return excluded;
            }

            var broadcaster = UsernameNormalizer.Normalize(serviceBackbone.BroadcasterName);
            if (!string.IsNullOrWhiteSpace(broadcaster))
            {
                excluded.Add(broadcaster);
            }

            var bot = UsernameNormalizer.Normalize(serviceBackbone.BotName ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(bot) && !excluded.Contains(bot, StringComparer.Ordinal))
            {
                excluded.Add(bot);
            }

            return excluded;
        }

        private bool IsExcludedUsername(string? normalizedUsername)
        {
            if (string.IsNullOrWhiteSpace(normalizedUsername) || serviceBackbone == null)
            {
                return false;
            }

            return serviceBackbone.IsBroadcasterOrBot(normalizedUsername);
        }

        private async Task<HashSet<string>> GetExcludedUserIdsAsync()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (twitchService == null)
            {
                return ids;
            }

            try
            {
                var broadcasterId = await twitchService.GetBroadcasterUserId();
                if (!string.IsNullOrWhiteSpace(broadcasterId))
                {
                    ids.Add(broadcasterId);
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not resolve broadcaster user ID for subscriber exclusion.");
            }

            try
            {
                var botId = await twitchService.GetBotUserId();
                if (!string.IsNullOrWhiteSpace(botId))
                {
                    ids.Add(botId);
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not resolve bot user ID for subscriber exclusion.");
            }

            return ids;
        }

        private static bool IsExcludedAccount(
            string? userId,
            string? username,
            HashSet<string> excludedUserIds,
            List<string> excludedUsernames)
        {
            if (!string.IsNullOrWhiteSpace(userId) && excludedUserIds.Contains(userId))
            {
                return true;
            }

            var normalized = UsernameNormalizer.Normalize(username ?? string.Empty);
            return !string.IsNullOrWhiteSpace(normalized) && excludedUsernames.Contains(normalized, StringComparer.Ordinal);
        }
    }
}
