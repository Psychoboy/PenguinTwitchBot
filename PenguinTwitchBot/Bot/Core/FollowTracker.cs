using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Bot.Core;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Core
{
    public class FollowTracker(
        ILogger<FollowTracker> logger,
        IServiceScopeFactory scopeFactory,
        ITwitchService twitchService) : IFollowTracker
    {
        private const int SyncBatchSize = 500;
        private const int SyncLiveFollowGraceSeconds = 30;
        private const int FollowEventMatchWindowSeconds = 5;

        private readonly SemaphoreSlim _recordLock = new(1, 1);
        private readonly SemaphoreSlim _syncLock = new(1, 1);
        private readonly HashSet<string> _inFlightUserIds = new(StringComparer.Ordinal);
        private volatile bool _isSyncInProgress;
        private FollowerSyncResult? _lastSyncResult;

        public bool IsSyncInProgress => _isSyncInProgress;
        public FollowerSyncResult? LastSyncResult => _lastSyncResult;
        public event Action? SyncStateChanged;

        public async Task<bool> TryBeginFollowDispatchAsync(
            string userId,
            string username,
            string displayName,
            DateTime followedAtUtc,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                logger.LogWarning("TryBeginFollowDispatchAsync called with empty UserId for {Username}; ignoring.", username);
                return false;
            }

            var normalizedUsername = UsernameNormalizer.Normalize(username);
            var utcFollowDate = NormalizeToUtc(followedAtUtc);

            await _recordLock.WaitAsync(cancellationToken);
            try
            {
                if (_inFlightUserIds.Contains(userId))
                {
                    logger.LogInformation(
                        "Follow dispatch for {UserLogin} ({UserId}) is already in progress; ignoring duplicate delivery.",
                        normalizedUsername,
                        userId);
                    return false;
                }

                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var existing = await db.Followers
                    .Find(f => f.UserId == userId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (existing != null)
                {
                    var changed = UpdateNamesIfChanged(existing, normalizedUsername, displayName);

                    if (!existing.FollowEventDispatched)
                    {
                        if (Math.Abs((existing.FollowDate - utcFollowDate).TotalSeconds) <= FollowEventMatchWindowSeconds)
                        {
                            if (changed)
                            {
                                db.Followers.Update(existing);
                                await db.SaveChangesAsync();
                            }

                            _inFlightUserIds.Add(userId);
                            return true;
                        }

                        // A different/later follow timestamp indicates a genuine unfollow + re-follow;
                        // permanently mark as dispatched so it remains suppressed.
                        existing.FollowEventDispatched = true;
                        changed = true;
                    }

                    if (changed)
                    {
                        db.Followers.Update(existing);
                        await db.SaveChangesAsync();
                    }

                    return false;
                }

                var follower = new Follower
                {
                    UserId = userId,
                    Username = normalizedUsername,
                    DisplayName = displayName ?? string.Empty,
                    FollowDate = utcFollowDate,
                    FollowEventDispatched = false
                };

                await db.Followers.AddAsync(follower);

                try
                {
                    await db.SaveChangesAsync();
                    _inFlightUserIds.Add(userId);
                    return true;
                }
                catch (DbUpdateException ex) when (ex.IsUniqueViolation())
                {
                    logger.LogInformation(
                        "Follower {UserLogin} ({UserId}) was recorded concurrently; treating as already followed.",
                        normalizedUsername,
                        userId);
                    return false;
                }
            }
            finally
            {
                _recordLock.Release();
            }
        }

        public async Task CompleteFollowDispatchAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            await _recordLock.WaitAsync(cancellationToken);
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var existing = await db.Followers
                    .Find(f => f.UserId == userId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (existing != null && !existing.FollowEventDispatched)
                {
                    existing.FollowEventDispatched = true;
                    db.Followers.Update(existing);
                    await db.SaveChangesAsync();
                }
            }
            finally
            {
                _inFlightUserIds.Remove(userId);
                _recordLock.Release();
            }
        }

        public void ReleaseFollowDispatch(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            _recordLock.Wait();
            try
            {
                _inFlightUserIds.Remove(userId);
            }
            finally
            {
                _recordLock.Release();
            }
        }

        public async Task<FollowerSyncResult> SyncFollowersAsync(
            IProgress<FollowerSyncProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!_syncLock.Wait(0))
            {
                throw new InvalidOperationException("A follower sync is already in progress.");
            }

            _isSyncInProgress = true;
            NotifySyncStateChanged();
            try
            {
                var syncStartedAtUtc = DateTime.UtcNow;
                var liveFollowCutoffUtc = syncStartedAtUtc.AddSeconds(-SyncLiveFollowGraceSeconds);

                logger.LogInformation("Starting manual follower sync from Twitch.");
                var twitchFollowers = await twitchService.GetAllFollowersAsync(progress, cancellationToken);
                var deduplicated = twitchFollowers
                    .Where(f => !string.IsNullOrWhiteSpace(f.UserId))
                    .DistinctBy(f => f.UserId, StringComparer.Ordinal)
                    .ToList();

                var addedCount = 0;
                var updatedCount = 0;
                var existingCount = 0;

                await _recordLock.WaitAsync(cancellationToken);
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                    var existingByUserId = await db.Followers
                        .Find(_ => true)
                        .ToDictionaryAsync(f => f.UserId, StringComparer.Ordinal, cancellationToken);

                    var pendingWrites = 0;

                    foreach (var item in deduplicated)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var normalizedUsername = UsernameNormalizer.Normalize(item.Username);
                        var displayName = item.DisplayName ?? string.Empty;

                        if (existingByUserId.TryGetValue(item.UserId, out var existing))
                        {
                            if (UpdateNamesIfChanged(existing, normalizedUsername, displayName))
                            {
                                db.Followers.Update(existing);
                                updatedCount++;
                                pendingWrites++;
                            }
                            else
                            {
                                existingCount++;
                            }
                        }
                        else
                        {
                            var followDate = NormalizeToUtc(item.FollowDate);
                            // Followers who followed right around/during this sync may still have an
                            // undelivered EventSub notification in flight; keep them eligible for that
                            // matching first-follow event while suppressing historical baseline followers.
                            var isHistoricalBaseline = followDate < liveFollowCutoffUtc;

                            var newFollower = new Follower
                            {
                                UserId = item.UserId,
                                Username = normalizedUsername,
                                DisplayName = displayName,
                                FollowDate = followDate,
                                FollowEventDispatched = isHistoricalBaseline
                            };

                            await db.Followers.AddAsync(newFollower);
                            existingByUserId[item.UserId] = newFollower;
                            addedCount++;
                            pendingWrites++;
                        }

                        if (pendingWrites >= SyncBatchSize)
                        {
                            await db.SaveChangesAsync();
                            pendingWrites = 0;
                        }
                    }

                    if (pendingWrites > 0)
                    {
                        await db.SaveChangesAsync();
                    }

                    var totalPersisted = existingByUserId.Count;
                    var result = new FollowerSyncResult(
                        TotalTwitchFollowers: deduplicated.Count,
                        AddedCount: addedCount,
                        UpdatedCount: updatedCount,
                        ExistingCount: existingCount,
                        TotalPersistedFollowers: totalPersisted,
                        CompletedAtUtc: DateTime.UtcNow);

                    _lastSyncResult = result;
                    logger.LogInformation(
                        "Manual follower sync completed: {TotalTwitch} from Twitch, {Added} added, {Updated} updated, {Existing} unchanged, {TotalPersisted} total persisted.",
                        result.TotalTwitchFollowers,
                        result.AddedCount,
                        result.UpdatedCount,
                        result.ExistingCount,
                        result.TotalPersistedFollowers);

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

        public async Task<int> GetPersistedFollowerCountAsync(CancellationToken cancellationToken = default)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.Followers.Find(_ => true).CountAsync(cancellationToken);
        }

        private void NotifySyncStateChanged()
        {
            try
            {
                SyncStateChanged?.Invoke();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error notifying SyncStateChanged subscribers.");
            }
        }

        private static bool UpdateNamesIfChanged(Follower existing, string normalizedUsername, string? displayName)
        {
            var changed = false;
            if (!string.IsNullOrEmpty(normalizedUsername) && existing.Username != normalizedUsername)
            {
                existing.Username = normalizedUsername;
                changed = true;
            }
            if (!string.IsNullOrEmpty(displayName) && existing.DisplayName != displayName)
            {
                existing.DisplayName = displayName;
                changed = true;
            }
            return changed;
        }

        private static DateTime NormalizeToUtc(DateTime value)
        {
            if (value == DateTime.MinValue)
            {
                return DateTime.UtcNow;
            }

            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }
    }
}
