using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Bot.Core;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Credits;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Overlay.Credits
{
    public sealed class CreditsEventCollectorService : ICreditsEventCollector, IHostedService, IDisposable
    {
        public const string RetentionSettingName = "CreditEventRetentionDays";
        public const int DefaultRetentionDays = 90;

        private static readonly TimeSpan ChatterFlushInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan RetentionCleanupInterval = TimeSpan.FromHours(12);

        private readonly IServiceBackbone _serviceBackbone;
        private readonly ITwitchService _twitchService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<CreditsEventCollectorService> _logger;

        private readonly ConcurrentDictionary<ChatterBucketKey, ChatterAccumulator> _chatterBuffer = new();
        private readonly SemaphoreSlim _flushLock = new(1, 1);
        private readonly SemaphoreSlim _sessionLock = new(1, 1);

        private Timer? _flushTimer;
        private Timer? _retentionTimer;
        private Guid? _currentStreamSessionId;
        private bool _disposed;

        public CreditsEventCollectorService(
            IServiceBackbone serviceBackbone,
            ITwitchService twitchService,
            IServiceScopeFactory scopeFactory,
            ILogger<CreditsEventCollectorService> logger,
            TimeProvider? timeProvider = null)
        {
            _serviceBackbone = serviceBackbone;
            _twitchService = twitchService;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        public Guid? CurrentStreamSessionId => _currentStreamSessionId;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _serviceBackbone.StreamStarted += OnStreamStarted;
            _serviceBackbone.StreamEnded += OnStreamEnded;
            _serviceBackbone.FollowEvent += OnFollow;
            _serviceBackbone.SubscriptionEvent += OnSubscription;
            _serviceBackbone.SubscriptionGiftEvent += OnSubscriptionGift;
            _serviceBackbone.CheerEvent += OnCheer;
            _serviceBackbone.BitsUseEvent += OnBitsUse;
            _serviceBackbone.IncomingRaidEvent += OnIncomingRaid;
            _serviceBackbone.OutgoingRaidEvent += OnOutgoingRaid;
            _serviceBackbone.ChannelPointRedeemEvent += OnChannelPointRedeem;
            _serviceBackbone.ChatNotificationEvent += OnChatNotification;

            await RecoverOrLoadActiveSessionAsync(cancellationToken);

            _flushTimer = new Timer(_ => _ = SafeFlushAsync(), null, ChatterFlushInterval, ChatterFlushInterval);
            _retentionTimer = new Timer(_ => _ = SafeRetentionCleanupAsync(), null, TimeSpan.FromMinutes(2), RetentionCleanupInterval);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _serviceBackbone.StreamStarted -= OnStreamStarted;
            _serviceBackbone.StreamEnded -= OnStreamEnded;
            _serviceBackbone.FollowEvent -= OnFollow;
            _serviceBackbone.SubscriptionEvent -= OnSubscription;
            _serviceBackbone.SubscriptionGiftEvent -= OnSubscriptionGift;
            _serviceBackbone.CheerEvent -= OnCheer;
            _serviceBackbone.BitsUseEvent -= OnBitsUse;
            _serviceBackbone.IncomingRaidEvent -= OnIncomingRaid;
            _serviceBackbone.OutgoingRaidEvent -= OnOutgoingRaid;
            _serviceBackbone.ChannelPointRedeemEvent -= OnChannelPointRedeem;
            _serviceBackbone.ChatNotificationEvent -= OnChatNotification;

            if (_flushTimer != null)
            {
                await _flushTimer.DisposeAsync();
                _flushTimer = null;
            }
            if (_retentionTimer != null)
            {
                await _retentionTimer.DisposeAsync();
                _retentionTimer = null;
            }

            await FlushPendingChattersAsync(cancellationToken);
        }

        public void RecordChatter(ChatMessageEventArgs chatMessage)
        {
            if (chatMessage == null || !chatMessage.FromOwnChannel)
                return;

            var normalizedLogin = UsernameNormalizer.Normalize(chatMessage.Name);
            var userId = chatMessage.UserId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(normalizedLogin))
                return;

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var sessionId = _currentStreamSessionId;
            var hourBucket = sessionId.HasValue ? 0L : nowUtc.Ticks / TimeSpan.TicksPerHour;
            var userKey = !string.IsNullOrWhiteSpace(userId) ? $"id:{userId}" : $"name:{normalizedLogin}";
            var bucketKey = new ChatterBucketKey(sessionId, hourBucket, userKey);

            _chatterBuffer.AddOrUpdate(
                bucketKey,
                _ => new ChatterAccumulator
                {
                    UserId = userId,
                    Username = normalizedLogin,
                    DisplayName = string.IsNullOrWhiteSpace(chatMessage.DisplayName) ? chatMessage.Name : chatMessage.DisplayName,
                    IsMod = chatMessage.IsMod,
                    IsVip = chatMessage.IsVip,
                    IsSub = chatMessage.IsSub,
                    IsBroadcaster = chatMessage.IsBroadcaster,
                    MessageCount = 1,
                    FirstSeenUtc = nowUtc,
                    LastSeenUtc = nowUtc,
                    Dirty = true
                },
                (_, existing) =>
                {
                    lock (existing)
                    {
                        if (!string.IsNullOrWhiteSpace(userId)) existing.UserId = userId;
                        if (!string.IsNullOrWhiteSpace(normalizedLogin)) existing.Username = normalizedLogin;
                        if (!string.IsNullOrWhiteSpace(chatMessage.DisplayName)) existing.DisplayName = chatMessage.DisplayName;
                        existing.IsMod |= chatMessage.IsMod;
                        existing.IsVip |= chatMessage.IsVip;
                        existing.IsSub |= chatMessage.IsSub;
                        existing.IsBroadcaster |= chatMessage.IsBroadcaster;
                        existing.MessageCount++;
                        existing.LastSeenUtc = nowUtc;
                        existing.Dirty = true;
                    }
                    return existing;
                });
        }

        public async Task FlushPendingChattersAsync(CancellationToken cancellationToken = default)
        {
            if (_chatterBuffer.IsEmpty) return;

            await _flushLock.WaitAsync(cancellationToken);
            try
            {
                var toFlush = new List<(ChatterBucketKey Key, ChatterAccumulator Acc, ChatterSnapshot Snap)>();
                foreach (var kvp in _chatterBuffer)
                {
                    ChatterSnapshot? snap = null;
                    lock (kvp.Value)
                    {
                        if (!kvp.Value.Dirty && kvp.Value.MessageCount <= kvp.Value.FlushedMessageCount)
                            continue;

                        snap = new ChatterSnapshot(
                            kvp.Value.PersistedEventId,
                            kvp.Value.UserId,
                            kvp.Value.Username,
                            kvp.Value.DisplayName,
                            kvp.Value.IsMod,
                            kvp.Value.IsVip,
                            kvp.Value.IsSub,
                            kvp.Value.IsBroadcaster,
                            kvp.Value.MessageCount,
                            kvp.Value.FlushedMessageCount,
                            kvp.Value.LastSeenUtc);
                    }
                    if (snap != null)
                        toFlush.Add((kvp.Key, kvp.Value, snap));
                }

                if (toFlush.Count == 0) return;

                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var persisted = new List<(ChatterAccumulator Acc, ChatterSnapshot Snap, CreditEvent Entity)>(toFlush.Count);

                foreach (var (key, acc, snap) in toFlush)
                {
                    var metadataJson = JsonSerializer.Serialize(new
                    {
                        isMod = snap.IsMod,
                        isVip = snap.IsVip,
                        isSub = snap.IsSub,
                        isBroadcaster = snap.IsBroadcaster
                    });

                    CreditEvent? existing = null;
                    if (snap.PersistedEventId.HasValue)
                    {
                        existing = await db.CreditEvents.GetByIdAsync(snap.PersistedEventId.Value);
                    }
                    else if (key.SessionId.HasValue && !string.IsNullOrWhiteSpace(snap.UserId))
                    {
                        existing = await db.CreditEvents
                            .Find(e => e.StreamSessionId == key.SessionId
                                    && e.EventType == CreditEventTypes.Chatter
                                    && e.UserId == snap.UserId)
                            .FirstOrDefaultAsync(cancellationToken);
                    }

                    var delta = Math.Max(0, snap.MessageCount - snap.FlushedMessageCount);
                    if (existing != null)
                    {
                        existing.Username = snap.Username;
                        existing.DisplayName = snap.DisplayName;
                        existing.Amount += delta;
                        existing.MetadataJson = metadataJson;
                        existing.OccurredAtUtc = snap.LastSeenUtc;
                        db.CreditEvents.Update(existing);
                        persisted.Add((acc, snap, existing));
                    }
                    else
                    {
                        var entity = new CreditEvent
                        {
                            EventType = CreditEventTypes.Chatter,
                            UserId = snap.UserId,
                            Username = snap.Username,
                            DisplayName = snap.DisplayName,
                            Amount = snap.MessageCount,
                            MetadataJson = metadataJson,
                            OccurredAtUtc = snap.LastSeenUtc,
                            StreamSessionId = key.SessionId
                        };
                        entity.NormalizeUsername();
                        await db.CreditEvents.AddAsync(entity);
                        persisted.Add((acc, snap, entity));
                    }
                }

                await db.SaveChangesAsync();

                foreach (var (acc, snap, entity) in persisted)
                {
                    lock (acc)
                    {
                        acc.PersistedEventId = entity.Id;
                        acc.FlushedMessageCount = Math.Max(acc.FlushedMessageCount, snap.MessageCount);
                        if (acc.MessageCount == snap.MessageCount)
                        {
                            acc.Dirty = false;
                        }
                    }
                }

                // Prune old offline hour buckets and inactive session buckets from memory
                var currentHourBucket = _timeProvider.GetUtcNow().UtcDateTime.Ticks / TimeSpan.TicksPerHour;
                foreach (var key in _chatterBuffer.Keys.ToList())
                {
                    var isExpiredOffline = !key.SessionId.HasValue && key.HourBucket < currentHourBucket - 1;
                    var isOldSession = key.SessionId.HasValue && key.SessionId != _currentStreamSessionId;
                    if (isExpiredOffline || isOldSession)
                    {
                        if (_chatterBuffer.TryGetValue(key, out var acc))
                        {
                            bool canRemove;
                            lock (acc) { canRemove = !acc.Dirty && acc.MessageCount <= acc.FlushedMessageCount; }
                            if (canRemove) _chatterBuffer.TryRemove(key, out _);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed flushing buffered chatter events to Credits.");
            }
            finally
            {
                _flushLock.Release();
            }
        }

        public async Task RecordCustomEventAsync(
            string eventType,
            string? userId,
            string? username,
            string? displayName,
            decimal amount = 1,
            string? detail = null,
            string? tier = null,
            string? avatarUrl = null,
            bool isManual = false,
            bool isAnonymous = false,
            Dictionary<string, object?>? metadata = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(eventType)) return;

            var normalizedUsername = UsernameNormalizer.Normalize(username ?? displayName ?? string.Empty);
            var safeDisplayName = !string.IsNullOrWhiteSpace(displayName)
                ? displayName.Trim()
                : (!string.IsNullOrWhiteSpace(username) ? username.Trim() : (isAnonymous ? "Anonymous" : "Unknown"));

            var creditEvent = new CreditEvent
            {
                EventType = eventType.Trim(),
                UserId = userId?.Trim() ?? string.Empty,
                Username = normalizedUsername,
                DisplayName = safeDisplayName,
                AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim(),
                Amount = amount,
                Tier = string.IsNullOrWhiteSpace(tier) ? null : tier.Trim(),
                Detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim(),
                MetadataJson = metadata is { Count: > 0 } ? JsonSerializer.Serialize(metadata) : null,
                OccurredAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
                StreamSessionId = _currentStreamSessionId,
                IsAnonymous = isAnonymous,
                IsManual = isManual
            };

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                await db.CreditEvents.AddAsync(creditEvent);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed recording credit event {EventType} for {Username}.",
                    CreditsService.SanitizeForLog(creditEvent.EventType),
                    CreditsService.SanitizeForLog(creditEvent.Username));
            }
        }

        public async Task<Guid> MarkManualCheckpointAsync(string? label = null, DateTime? checkpointUtc = null, CancellationToken cancellationToken = default)
        {
            var now = checkpointUtc ?? _timeProvider.GetUtcNow().UtcDateTime;
            var session = new CreditStreamSession
            {
                StartedAtUtc = now,
                Title = string.IsNullOrWhiteSpace(label) ? $"Checkpoint ({now:yyyy-MM-dd HH:mm} UTC)" : label.Trim(),
                IsManualCheckpoint = true
            };

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await db.CreditStreamSessions.AddAsync(session);
            await db.SaveChangesAsync();
            return session.Id;
        }

        public async Task<int> CleanupOldEventsAsync(int retentionDays, CancellationToken cancellationToken = default)
        {
            if (retentionDays <= 0) return 0;

            var cutoffUtc = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-retentionDays);
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            return await db.CreditEvents
                .Find(e => !e.IsManual && e.OccurredAtUtc < cutoffUtc)
                .ExecuteDeleteAsync(cancellationToken);
        }

        internal Task OnStreamStarted(object? sender, EventArgs e) => EnsureStreamSessionStartedAsync();

        internal async Task OnStreamEnded(object? sender, EventArgs e)
        {
            await FlushPendingChattersAsync();
            await _sessionLock.WaitAsync();
            try
            {
                var sessionId = _currentStreamSessionId;
                _currentStreamSessionId = null;

                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var now = _timeProvider.GetUtcNow().UtcDateTime;

                if (sessionId.HasValue)
                {
                    var session = await db.CreditStreamSessions.GetByIdAsync(sessionId.Value);
                    if (session != null && session.EndedAtUtc == null)
                    {
                        session.EndedAtUtc = now;
                        db.CreditStreamSessions.Update(session);
                    }
                }
                else
                {
                    var openSessions = await db.CreditStreamSessions
                        .Find(s => !s.IsManualCheckpoint && s.EndedAtUtc == null)
                        .ToListAsync();
                    foreach (var s in openSessions)
                    {
                        s.EndedAtUtc = now;
                        db.CreditStreamSessions.Update(s);
                    }
                }

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed closing credit stream session on stream end.");
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        internal Task OnFollow(object? sender, FollowEventArgs e)
        {
            return RecordCustomEventAsync(
                CreditEventTypes.Follow,
                e.UserId,
                e.Username,
                e.DisplayName,
                amount: 1);
        }

        internal Task OnSubscription(object? sender, SubscriptionEventArgs e)
        {
            string eventType;
            decimal amount;
            string? detail = null;

            if (e.IsRenewal)
            {
                eventType = CreditEventTypes.Resubscribe;
                amount = e.Count ?? 1;
                detail = $"{amount} month(s)";
            }
            else if (e.IsGift)
            {
                eventType = CreditEventTypes.GiftedSubRecipient;
                amount = 1;
                detail = "Gifted Sub";
            }
            else
            {
                eventType = CreditEventTypes.Subscribe;
                amount = 1;
                detail = string.IsNullOrWhiteSpace(e.Tier) ? "Sub" : FormatTier(e.Tier);
            }

            var meta = new Dictionary<string, object?>
            {
                ["streak"] = e.Streak,
                ["hadPreviousSub"] = e.HadPreviousSub,
                ["message"] = e.Message
            };

            return RecordCustomEventAsync(
                eventType,
                e.UserId,
                e.Name,
                e.DisplayName,
                amount: amount,
                detail: detail,
                tier: e.Tier,
                metadata: meta);
        }

        internal Task OnSubscriptionGift(object? sender, SubscriptionGiftEventArgs e)
        {
            var meta = new Dictionary<string, object?>
            {
                ["totalGifted"] = e.TotalGifted
            };

            return RecordCustomEventAsync(
                CreditEventTypes.SubGift,
                e.UserId,
                e.Name,
                e.DisplayName,
                amount: Math.Max(1, e.GiftAmount),
                detail: $"{Math.Max(1, e.GiftAmount)} gifted",
                isAnonymous: string.IsNullOrWhiteSpace(e.UserId) && string.IsNullOrWhiteSpace(e.Name),
                metadata: meta);
        }

        internal Task OnCheer(object? sender, CheerEventArgs e)
        {
            return RecordCustomEventAsync(
                CreditEventTypes.Cheer,
                e.UserId,
                e.Name,
                e.DisplayName,
                amount: Math.Max(0, e.Amount),
                detail: $"{e.Amount} bits",
                isAnonymous: e.IsAnonymous,
                metadata: string.IsNullOrWhiteSpace(e.Message)
                    ? null
                    : new Dictionary<string, object?> { ["message"] = e.Message });
        }

        internal Task OnBitsUse(object? sender, BitsUseEventArgs e)
        {
            // channel.cheer already captures standard cheers; skip "cheer" to avoid double counting.
            if (string.Equals(e.Type, "cheer", StringComparison.OrdinalIgnoreCase))
                return Task.CompletedTask;

            var detail = !string.IsNullOrWhiteSpace(e.CustomPowerUp?.Title)
                ? e.CustomPowerUp.Title
                : !string.IsNullOrWhiteSpace(e.PowerUp?.Type)
                    ? e.PowerUp.Type.Replace('_', ' ')
                    : e.Type;

            return RecordCustomEventAsync(
                CreditEventTypes.BitsPowerUp,
                e.UserId,
                e.Name,
                e.DisplayName,
                amount: Math.Max(0, e.Amount),
                detail: detail,
                metadata: string.IsNullOrWhiteSpace(e.Message)
                    ? null
                    : new Dictionary<string, object?> { ["message"] = e.Message });
        }

        internal Task OnIncomingRaid(object? sender, RaidEventArgs e)
        {
            return RecordCustomEventAsync(
                CreditEventTypes.IncomingRaid,
                e.UserId,
                e.Name,
                e.DisplayName,
                amount: Math.Max(0, e.NumberOfViewers),
                detail: $"{e.NumberOfViewers} viewers");
        }

        internal Task OnOutgoingRaid(object? sender, OutgoingRaidEventArgs e)
        {
            return RecordCustomEventAsync(
                CreditEventTypes.OutgoingRaid,
                e.TargetUserId,
                e.TargetUserName,
                e.TargetDisplayName,
                amount: Math.Max(0, e.NumberOfViewers),
                detail: $"Raided {e.TargetDisplayName}");
        }

        internal Task OnChannelPointRedeem(object? sender, ChannelPointRedeemEventArgs e)
        {
            var meta = string.IsNullOrWhiteSpace(e.UserInput)
                ? null
                : new Dictionary<string, object?> { ["userInput"] = e.UserInput };

            return RecordCustomEventAsync(
                CreditEventTypes.ChannelPointRedeem,
                e.UserId,
                string.IsNullOrWhiteSpace(e.Username) ? e.Sender : e.Username,
                e.Sender,
                amount: 1,
                detail: e.Title,
                metadata: meta);
        }

        internal Task OnChatNotification(object? sender, ChatNotificationEventArgs e)
        {
            var notice = (e.NoticeType ?? string.Empty).Trim().ToLowerInvariant();
            switch (notice)
            {
                case "watch_streak":
                {
                    var streak = e.WatchStreak?.StreakCount ?? 1;
                    return RecordCustomEventAsync(
                        CreditEventTypes.WatchStreak,
                        e.UserId,
                        e.Name,
                        e.DisplayName,
                        amount: streak,
                        detail: $"{streak} stream streak",
                        isAnonymous: e.IsAnonymous);
                }
                case "charity_donation":
                {
                    decimal amount = 0;
                    var detail = "Charity Donation";
                    string? currency = null;
                    if (e.CharityDonation != null)
                    {
                        var divisor = (decimal)Math.Pow(10, Math.Clamp(e.CharityDonation.AmountDecimalPlaces, 0, 6));
                        amount = divisor > 0 ? e.CharityDonation.AmountValue / divisor : e.CharityDonation.AmountValue;
                        currency = e.CharityDonation.AmountCurrency;
                        detail = string.IsNullOrWhiteSpace(e.CharityDonation.CharityName)
                            ? $"{amount.ToString("0.##", CultureInfo.InvariantCulture)} {currency}".Trim()
                            : $"{e.CharityDonation.CharityName} ({amount.ToString("0.##", CultureInfo.InvariantCulture)} {currency})".Trim();
                    }

                    return RecordCustomEventAsync(
                        CreditEventTypes.CharityDonation,
                        e.UserId,
                        e.Name,
                        e.DisplayName,
                        amount: amount,
                        detail: detail,
                        tier: currency,
                        isAnonymous: e.IsAnonymous);
                }
                case "prime_paid_upgrade":
                    return RecordCustomEventAsync(
                        CreditEventTypes.SubUpgrade,
                        e.UserId,
                        e.Name,
                        e.DisplayName,
                        amount: 1,
                        detail: "Prime Paid Upgrade",
                        tier: e.PrimePaidUpgrade?.SubTier,
                        isAnonymous: e.IsAnonymous);
                case "gift_paid_upgrade":
                    return RecordCustomEventAsync(
                        CreditEventTypes.SubUpgrade,
                        e.UserId,
                        e.Name,
                        e.DisplayName,
                        amount: 1,
                        detail: "Gift Paid Upgrade",
                        isAnonymous: e.IsAnonymous);
                case "pay_it_forward":
                    return RecordCustomEventAsync(
                        CreditEventTypes.PayItForward,
                        e.UserId,
                        e.Name,
                        e.DisplayName,
                        amount: 1,
                        detail: "Paid It Forward",
                        isAnonymous: e.IsAnonymous);
                case "bits_badge_tier":
                {
                    var tier = e.BitsBadgeTier?.Tier ?? 0;
                    return RecordCustomEventAsync(
                        CreditEventTypes.BitsBadgeTier,
                        e.UserId,
                        e.Name,
                        e.DisplayName,
                        amount: tier,
                        detail: $"{tier} Bits Badge",
                        isAnonymous: e.IsAnonymous);
                }
                default:
                    // Ignore sub, resub, sub_gift, community_sub_gift, raid, announcement, etc.
                    // Dedicated EventSub subscriptions handle those without duplicates.
                    return Task.CompletedTask;
            }
        }

        private async Task RecoverOrLoadActiveSessionAsync(CancellationToken cancellationToken)
        {
            try
            {
                var isOnline = await _twitchService.IsStreamOnline();
                if (!isOnline)
                {
                    await _sessionLock.WaitAsync(cancellationToken);
                    try
                    {
                        if (_currentStreamSessionId.HasValue || await _twitchService.IsStreamOnline())
                            return;

                        // Close any stale unclosed non-checkpoint sessions left over from a crash when stream is now offline.
                        await using var scope = _scopeFactory.CreateAsyncScope();
                        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                        var staleOpen = await db.CreditStreamSessions
                            .Find(s => !s.IsManualCheckpoint && s.EndedAtUtc == null)
                            .ToListAsync(cancellationToken);
                        if (staleOpen.Count > 0)
                        {
                            var now = _timeProvider.GetUtcNow().UtcDateTime;
                            foreach (var s in staleOpen)
                            {
                                s.EndedAtUtc = now;
                                db.CreditStreamSessions.Update(s);
                            }
                            await db.SaveChangesAsync();
                        }
                    }
                    finally
                    {
                        _sessionLock.Release();
                    }
                    return;
                }

                var startedAt = await _twitchService.StreamStartedAt();
                var startUtc = startedAt != DateTime.MinValue
                    ? DateTime.SpecifyKind(startedAt, DateTimeKind.Utc)
                    : _timeProvider.GetUtcNow().UtcDateTime;

                await EnsureStreamSessionStartedAsync(startUtc, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not verify live stream session on Credits collector startup.");
            }
        }

        private async Task EnsureStreamSessionStartedAsync(DateTime? explicitStartUtc = null, CancellationToken cancellationToken = default)
        {
            await _sessionLock.WaitAsync(cancellationToken);
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var targetStart = explicitStartUtc ?? now;

                var existingOpen = await db.CreditStreamSessions
                    .Find(s => !s.IsManualCheckpoint && s.EndedAtUtc == null)
                    .OrderByDescending(s => s.StartedAtUtc)
                    .FirstOrDefaultAsync(cancellationToken);

                if (existingOpen != null)
                {
                    // Reuse if it matches the live stream window (within 30 minutes)
                    if (Math.Abs((existingOpen.StartedAtUtc - targetStart).TotalMinutes) < 30 || !explicitStartUtc.HasValue)
                    {
                        _currentStreamSessionId = existingOpen.Id;
                        return;
                    }

                    existingOpen.EndedAtUtc = targetStart;
                    db.CreditStreamSessions.Update(existingOpen);
                }

                string? title = null;
                string? game = null;
                try
                {
                    title = await _twitchService.GetStreamTitle();
                    game = await _twitchService.GetCurrentGame();
                }
                catch { /* Non-fatal metadata fetch */ }

                var session = new CreditStreamSession
                {
                    StartedAtUtc = targetStart,
                    Title = string.IsNullOrWhiteSpace(title) ? null : title,
                    GameName = string.IsNullOrWhiteSpace(game) ? null : game,
                    IsManualCheckpoint = false
                };

                await db.CreditStreamSessions.AddAsync(session);
                await db.SaveChangesAsync();
                _currentStreamSessionId = session.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed starting credit stream session.");
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        private async Task SafeFlushAsync()
        {
            if (_disposed) return;
            try
            {
                await FlushPendingChattersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Periodic chatter flush skipped.");
            }
        }

        private async Task SafeRetentionCleanupAsync()
        {
            if (_disposed) return;
            try
            {
                var retentionDays = DefaultRetentionDays;
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var setting = await db.Settings.Find(s => s.Name == RetentionSettingName).FirstOrDefaultAsync();
                    if (setting?.IntSetting is int configured)
                    {
                        retentionDays = configured;
                    }
                }

                if (retentionDays > 0)
                {
                    var deleted = await CleanupOldEventsAsync(retentionDays);
                    if (deleted > 0)
                    {
                        _logger.LogInformation("Credits retention cleanup removed {Count} event(s) older than {Days} day(s).", deleted, retentionDays);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Credits retention cleanup failed.");
            }
        }

        private static string FormatTier(string? tier) => tier switch
        {
            "1000" => "Tier 1",
            "2000" => "Tier 2",
            "3000" => "Tier 3",
            "Prime" => "Prime",
            _ => tier ?? "Sub"
        };

        public void Dispose()
        {
            _disposed = true;
            _flushTimer?.Dispose();
            _retentionTimer?.Dispose();
            _flushLock.Dispose();
            _sessionLock.Dispose();
        }

        private readonly record struct ChatterBucketKey(Guid? SessionId, long HourBucket, string UserKey);

        private sealed class ChatterAccumulator
        {
            public Guid? PersistedEventId { get; set; }
            public string UserId { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public bool IsMod { get; set; }
            public bool IsVip { get; set; }
            public bool IsSub { get; set; }
            public bool IsBroadcaster { get; set; }
            public int MessageCount { get; set; }
            public int FlushedMessageCount { get; set; }
            public DateTime FirstSeenUtc { get; set; }
            public DateTime LastSeenUtc { get; set; }
            public bool Dirty { get; set; }
        }

        private sealed record ChatterSnapshot(
            Guid? PersistedEventId,
            string UserId,
            string Username,
            string DisplayName,
            bool IsMod,
            bool IsVip,
            bool IsSub,
            bool IsBroadcaster,
            int MessageCount,
            int FlushedMessageCount,
            DateTime LastSeenUtc);
    }

    public sealed class CreditsReceivedChatHandler(ICreditsEventCollector collector)
        : INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            if (notification.EventArgs != null)
            {
                collector.RecordChatter(notification.EventArgs);
            }
            return Task.CompletedTask;
        }
    }
}
