using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Bot.Core;
using PenguinTwitchBot.Database.Repository;
using System.Text.Json;

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
        public const string CheckLocalTableFirstSettingName = "Followers.CheckLocalTableFirst";
        public const string SyncScheduleSettingName = "Followers.SyncSchedule";

        private readonly SemaphoreSlim _recordLock = new(1, 1);
        private readonly SemaphoreSlim _syncLock = new(1, 1);
        private readonly HashSet<string> _inFlightUserIds = new(StringComparer.Ordinal);
        private volatile bool _isSyncInProgress;
        private FollowerSyncResult? _lastSyncResult;
        private bool? _cachedCheckLocalTableFirst;

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
            bool removeUnfollowed = false,
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

                logger.LogInformation("Starting follower sync from Twitch (removeUnfollowed={RemoveUnfollowed}).", removeUnfollowed);
                var twitchFollowers = await twitchService.GetAllFollowersAsync(progress, cancellationToken);
                var deduplicated = twitchFollowers
                    .Where(f => !string.IsNullOrWhiteSpace(f.UserId))
                    .DistinctBy(f => f.UserId, StringComparer.Ordinal)
                    .ToList();

                var addedCount = 0;
                var updatedCount = 0;
                var existingCount = 0;
                var removedCount = 0;

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

                    if (removeUnfollowed)
                    {
                        var twitchUserIds = deduplicated
                            .Select(f => f.UserId)
                            .ToHashSet(StringComparer.Ordinal);

                        foreach (var existing in existingByUserId.Values.ToList())
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (twitchUserIds.Contains(existing.UserId))
                            {
                                continue;
                            }

                            // Protect live followers who followed during this sync from being removed.
                            if (_inFlightUserIds.Contains(existing.UserId) ||
                                existing.FollowDate >= liveFollowCutoffUtc)
                            {
                                continue;
                            }

                            db.Followers.Remove(existing);
                            existingByUserId.Remove(existing.UserId);
                            removedCount++;
                            pendingWrites++;

                            if (pendingWrites >= SyncBatchSize)
                            {
                                await db.SaveChangesAsync();
                                pendingWrites = 0;
                            }
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
                        CompletedAtUtc: DateTime.UtcNow,
                        RemovedCount: removedCount,
                        RemovedUnfollowed: removeUnfollowed);

                    _lastSyncResult = result;
                    logger.LogInformation(
                        "Follower sync completed (removeUnfollowed={RemoveUnfollowed}): {TotalTwitch} from Twitch, {Added} added, {Updated} updated, {Existing} unchanged, {Removed} removed, {TotalPersisted} total persisted.",
                        removeUnfollowed,
                        result.TotalTwitchFollowers,
                        result.AddedCount,
                        result.UpdatedCount,
                        result.ExistingCount,
                        result.RemovedCount,
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

        public async Task<bool> GetCheckLocalTableFirstAsync(CancellationToken cancellationToken = default)
        {
            var cached = _cachedCheckLocalTableFirst;
            if (cached.HasValue)
            {
                return cached.Value;
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var setting = await db.Settings
                .Find(s => s.Name == CheckLocalTableFirstSettingName)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            var value = setting == null || !bool.TryParse(setting.StringSetting, out var parsed)
                ? true
                : parsed;

            _cachedCheckLocalTableFirst = value;
            return value;
        }

        public async Task SetCheckLocalTableFirstAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var setting = await db.Settings
                .Find(s => s.Name == CheckLocalTableFirstSettingName)
                .FirstOrDefaultAsync(cancellationToken);

            if (setting == null)
            {
                await db.Settings.AddAsync(new Setting
                {
                    Name = CheckLocalTableFirstSettingName,
                    DataType = Setting.DataTypeEnum.String,
                    StringSetting = enabled.ToString()
                });
            }
            else
            {
                setting.DataType = Setting.DataTypeEnum.String;
                setting.StringSetting = enabled.ToString();
                db.Settings.Update(setting);
            }

            await db.SaveChangesAsync();
            _cachedCheckLocalTableFirst = enabled;
        }

        public async Task<FollowerSyncScheduleConfig> GetScheduleConfigAsync(CancellationToken cancellationToken = default)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var setting = await db.Settings
                .Find(s => s.Name == SyncScheduleSettingName)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            if (setting == null || string.IsNullOrWhiteSpace(setting.StringSetting))
            {
                return new FollowerSyncScheduleConfig();
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<FollowerSyncScheduleConfig>(setting.StringSetting);
                if (parsed == null)
                {
                    return new FollowerSyncScheduleConfig();
                }

                return parsed with { IntervalDays = Math.Clamp(parsed.IntervalDays, 1, 365) };
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Invalid follower sync schedule JSON; falling back to default.");
                return new FollowerSyncScheduleConfig();
            }
        }

        public async Task SaveScheduleConfigAsync(FollowerSyncScheduleConfig config, CancellationToken cancellationToken = default)
        {
            var sanitized = config with
            {
                IntervalDays = Math.Clamp(config.IntervalDays, 1, 365)
            };

            var json = JsonSerializer.Serialize(sanitized);

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var setting = await db.Settings
                .Find(s => s.Name == SyncScheduleSettingName)
                .FirstOrDefaultAsync(cancellationToken);

            if (setting == null)
            {
                await db.Settings.AddAsync(new Setting
                {
                    Name = SyncScheduleSettingName,
                    DataType = Setting.DataTypeEnum.String,
                    StringSetting = json
                });
            }
            else
            {
                setting.DataType = Setting.DataTypeEnum.String;
                setting.StringSetting = json;
                db.Settings.Update(setting);
            }

            await db.SaveChangesAsync();
        }

        public async Task<bool> TryRunScheduledSyncIfDueAsync(DateTime? nowUtc = null, CancellationToken cancellationToken = default)
        {
            var now = nowUtc ?? DateTime.UtcNow;
            var config = await GetScheduleConfigAsync(cancellationToken);
            if (!config.Enabled || config.IntervalDays <= 0)
            {
                return false;
            }

            if (config.LastScheduledRunUtc.HasValue &&
                now < config.LastScheduledRunUtc.Value.AddDays(config.IntervalDays))
            {
                return false;
            }

            if (_isSyncInProgress)
            {
                return false;
            }

            logger.LogInformation(
                "Scheduled follower sync is due (interval={IntervalDays}d, removeUnfollowed={RemoveUnfollowed}); starting sync.",
                config.IntervalDays,
                config.RemoveUnfollowed);

            await SyncFollowersAsync(
                removeUnfollowed: config.RemoveUnfollowed,
                cancellationToken: cancellationToken);

            await SaveScheduleConfigAsync(
                config with { LastScheduledRunUtc = now },
                cancellationToken);

            return true;
        }

        public async Task<Follower?> GetPersistedFollowerByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default)
        {
            var normalized = UsernameNormalizer.Normalize(username);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.Followers
                .Find(f => f.Username == normalized)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<PersistedFollowerPage> GetPersistedFollowersPageAsync(
            int page,
            int pageSize,
            string? search = null,
            string? sortBy = null,
            bool sortDescending = true,
            CancellationToken cancellationToken = default)
        {
            var safePage = Math.Max(0, page);
            var safePageSize = Math.Clamp(pageSize, 1, 100);

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var query = db.Followers.Find(_ => true).AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = UsernameNormalizer.Normalize(search);
                query = query.Where(f =>
                    f.Username.Contains(normalizedSearch) ||
                    f.DisplayName.ToLower().Contains(normalizedSearch));
            }

            var totalCount = await query.CountAsync(cancellationToken);

            query = (sortBy?.ToLowerInvariant(), sortDescending) switch
            {
                ("username", false) => query.OrderBy(f => f.Username).ThenByDescending(f => f.FollowDate),
                ("username", true) => query.OrderByDescending(f => f.Username).ThenByDescending(f => f.FollowDate),
                ("followdate", false) => query.OrderBy(f => f.FollowDate).ThenBy(f => f.Username),
                _ => query.OrderByDescending(f => f.FollowDate).ThenBy(f => f.Username)
            };

            var followers = await query
                .Skip(safePage * safePageSize)
                .Take(safePageSize)
                .ToListAsync(cancellationToken);

            if (followers.Count == 0)
            {
                return new PersistedFollowerPage([], totalCount);
            }

            var userIds = followers.Select(f => f.UserId).ToList();
            var viewersByUserId = await db.Viewers
                .Find(v => userIds.Contains(v.UserId))
                .AsNoTracking()
                .ToDictionaryAsync(v => v.UserId, StringComparer.Ordinal, cancellationToken);

            var items = followers.Select(f =>
            {
                viewersByUserId.TryGetValue(f.UserId, out var viewer);
                DateTime? lastSeen = viewer != null && viewer.LastSeen != DateTime.MinValue
                    ? viewer.LastSeen
                    : null;

                return new PersistedFollowerListItem(
                    FollowerId: f.Id,
                    UserId: f.UserId,
                    Username: f.Username,
                    DisplayName: f.DisplayName,
                    FollowDate: f.FollowDate,
                    ViewerId: viewer?.Id,
                    LastSeen: lastSeen,
                    IsSub: viewer?.isSub ?? false,
                    IsVip: viewer?.isVip ?? false,
                    IsMod: viewer?.isMod ?? false,
                    IsBroadcaster: viewer?.isBroadcaster ?? false);
            }).ToList();

            return new PersistedFollowerPage(items, totalCount);
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
