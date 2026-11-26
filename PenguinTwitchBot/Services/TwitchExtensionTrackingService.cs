using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Controllers;
using PenguinTwitchBot.Models;
using Prometheus;

namespace PenguinTwitchBot.Services;

public sealed class TwitchExtensionTrackingService : ITwitchExtensionTrackingService, IHostedService, IDisposable
{
    public static readonly TimeSpan DefaultActiveSessionTimeout = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan DefaultReengagementLogCooldown = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromSeconds(30);

    internal static readonly Prometheus.Gauge ActiveUsers = Metrics.CreateGauge(
        "twitch_extension_active_users",
        "Current number of active Twitch Extension users within the active session window",
        ["identity_shared", "role"]);

    internal static readonly Prometheus.Gauge ActiveUsersByFeature = Metrics.CreateGauge(
        "twitch_extension_active_users_by_feature",
        "Current number of active Twitch Extension users grouped by their last active feature",
        ["feature"]);

    internal static readonly Prometheus.Gauge UniqueUsersSinceRestart = Metrics.CreateGauge(
        "twitch_extension_unique_users_since_restart",
        "Distinct Twitch Extension users seen since bot startup",
        ["identity_shared"]);

    internal static readonly Prometheus.Counter SessionsStartedTotal = Metrics.CreateCounter(
        "twitch_extension_sessions_started_total",
        "Total Twitch Extension sessions started or resumed after inactivity",
        ["identity_shared"]);

    internal static readonly Prometheus.Counter RequestsTotal = Metrics.CreateCounter(
        "twitch_extension_requests_total",
        "Total Twitch Extension API requests handled",
        ["feature", "endpoint", "method", "status_code", "identity_shared"]);

    internal static readonly Prometheus.Counter ActionsTotal = Metrics.CreateCounter(
        "twitch_extension_actions_total",
        "Total interactive POST actions performed via the Twitch Extension",
        ["action", "outcome"]);

    internal static readonly Prometheus.Counter AuthFailuresTotal = Metrics.CreateCounter(
        "twitch_extension_auth_failures_total",
        "Total Twitch Extension JWT validation failures",
        ["reason"]);

    private static readonly HashSet<string> AllowedFeatures = new(StringComparer.OrdinalIgnoreCase)
    {
        "features", "leaderboards", "giveaway", "fishing", "starship", "commands", "other"
    };

    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "viewer", "broadcaster", "moderator", "external", "other"
    };

    private static readonly TimeSpan UsernameCacheTtl = TimeSpan.FromHours(6);
    private const int MaxUsernameCacheEntries = 10_000;
    private sealed record CachedUsernameEntry(string Username, DateTime CachedAtUtc);

    private readonly Dictionary<string, TwitchExtensionSession> _activeSessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> _lastLoggedActivityUtc = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CachedUsernameEntry> _usernameCache = new(StringComparer.Ordinal);
    private readonly HashSet<string> _uniqueSharedUsers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _uniqueAnonymousUsers = new(StringComparer.Ordinal);
    private readonly HashSet<(string IdentityShared, string Role)> _publishedActiveUserLabels = [];
    private readonly HashSet<string> _publishedFeatureLabels = [];
    private readonly object _stateLock = new();
    private readonly object _metricsLock = new();

    private readonly ILogger<TwitchExtensionTrackingService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _activeSessionTimeout;
    private readonly TimeSpan _reengagementLogCooldown;

    private CancellationTokenSource? _cleanupCts;
    private Task? _cleanupTask;
    private bool _disposed;

    public event EventHandler? SessionsChanged;

    // Used for deterministic unit testing
    internal Task? LastBackgroundTask { get; private set; }

    public TwitchExtensionTrackingService(
        ILogger<TwitchExtensionTrackingService> logger,
        IConfiguration? configuration = null,
        TimeProvider? timeProvider = null)
    {
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;

        var timeoutSeconds = configuration?.GetValue<int?>("TwitchExtension:Tracking:ActiveSessionTimeoutSeconds");
        _activeSessionTimeout = timeoutSeconds is > 0
            ? TimeSpan.FromSeconds(timeoutSeconds.Value)
            : DefaultActiveSessionTimeout;

        var cooldownMinutes = configuration?.GetValue<int?>("TwitchExtension:Tracking:ReengagementLogCooldownMinutes");
        _reengagementLogCooldown = cooldownMinutes is > 0
            ? TimeSpan.FromMinutes(cooldownMinutes.Value)
            : DefaultReengagementLogCooldown;

        try
        {
            Metrics.DefaultRegistry.AddBeforeCollectCallback(UpdateGaugeMetrics);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register Prometheus before-collect callback for Twitch Extension metrics");
        }
    }

    public IReadOnlyCollection<TwitchExtensionSession> GetActiveSessions()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var pruned = false;
        List<TwitchExtensionSession> snapshot;

        lock (_stateLock)
        {
            pruned = PruneExpiredSessionsLocked(now);
            snapshot = _activeSessions.Values
                .OrderByDescending(s => s.LastSeenUtc)
                .Select(CloneSession)
                .ToList();
        }

        if (pruned)
        {
            NotifySessionsChanged();
        }

        return snapshot;
    }

    public void RecordRequest(
        TwitchExtensionClaims claims,
        string feature,
        string endpoint,
        string httpMethod,
        int statusCode,
        string? clientIp,
        string? actionName = null,
        Func<Task<string?>>? resolveUsernameAsync = null)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var normalizedFeature = NormalizeFeature(feature);
        var normalizedEndpoint = string.IsNullOrWhiteSpace(endpoint) ? "unknown" : endpoint.Trim().ToLowerInvariant();
        var normalizedMethod = string.IsNullOrWhiteSpace(httpMethod) ? "GET" : httpMethod.Trim().ToUpperInvariant();
        var normalizedRole = NormalizeRole(claims.Role);
        var identitySharedLabel = claims.IsIdentityShared ? "true" : "false";
        var statusCodeLabel = statusCode > 0 ? statusCode.ToString() : "unknown";

        RequestsTotal
            .WithLabels(normalizedFeature, normalizedEndpoint, normalizedMethod, statusCodeLabel, identitySharedLabel)
            .Inc();

        string? actionOutcome = null;
        if (!string.IsNullOrWhiteSpace(actionName))
        {
            actionOutcome = MapStatusCodeToOutcome(statusCode);
            ActionsTotal.WithLabels(actionName.Trim().ToLowerInvariant(), actionOutcome).Inc();
        }

        var sessionKey = GetSessionKey(claims);
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            return;
        }

        var safeIp = string.IsNullOrWhiteSpace(clientIp) ? "Unknown" : clientIp;
        bool shouldLogSessionStart;
        bool needsUsernameResolution = false;
        string? cachedUsername = null;

        if (claims.IsIdentityShared && !string.IsNullOrWhiteSpace(claims.UserId) &&
            _usernameCache.TryGetValue(claims.UserId, out var cachedEntry))
        {
            if ((now - cachedEntry.CachedAtUtc) <= UsernameCacheTtl)
            {
                cachedUsername = cachedEntry.Username;
            }
            else
            {
                _usernameCache.TryRemove(new KeyValuePair<string, CachedUsernameEntry>(claims.UserId, cachedEntry));
            }
        }

        lock (_stateLock)
        {
            if (claims.IsIdentityShared && !string.IsNullOrWhiteSpace(claims.UserId))
            {
                _uniqueSharedUsers.Add(claims.UserId);
            }
            else
            {
                _uniqueAnonymousUsers.Add(sessionKey);
            }

            bool isNewSession;
            if (_activeSessions.TryGetValue(sessionKey, out var existing) &&
                (now - existing.LastSeenUtc) <= _activeSessionTimeout)
            {
                isNewSession = false;
                existing.LastSeenUtc = now;
                existing.LastFeature = normalizedFeature;
                existing.LastEndpoint = normalizedEndpoint;
                existing.Role = normalizedRole;
                existing.RequestCount++;
                if (!string.IsNullOrWhiteSpace(clientIp))
                {
                    existing.UserIp = safeIp;
                }

                if (claims.IsIdentityShared && !string.IsNullOrWhiteSpace(cachedUsername))
                {
                    existing.Username = cachedUsername;
                }
            }
            else
            {
                isNewSession = true;
                var initialUsername = claims.IsIdentityShared
                    ? (cachedUsername ?? claims.UserId ?? "Unknown")
                    : "Anonymous";

                _activeSessions[sessionKey] = new TwitchExtensionSession
                {
                    SessionKey = sessionKey,
                    UserId = claims.UserId,
                    OpaqueUserId = claims.OpaqueUserId,
                    Username = initialUsername,
                    Role = normalizedRole,
                    IsIdentityShared = claims.IsIdentityShared,
                    UserIp = safeIp,
                    LastFeature = normalizedFeature,
                    LastEndpoint = normalizedEndpoint,
                    FirstSeenUtc = now,
                    LastSeenUtc = now,
                    RequestCount = 1
                };
            }

            if (isNewSession)
            {
                SessionsStartedTotal.WithLabels(identitySharedLabel).Inc();
            }

            shouldLogSessionStart = !_lastLoggedActivityUtc.TryGetValue(sessionKey, out var lastLoggedUtc)
                                    || (now - lastLoggedUtc) >= _reengagementLogCooldown;

            _lastLoggedActivityUtc[sessionKey] = now;

            if (claims.IsIdentityShared
                && !string.IsNullOrWhiteSpace(claims.UserId)
                && string.IsNullOrWhiteSpace(cachedUsername)
                && resolveUsernameAsync != null)
            {
                needsUsernameResolution = true;
            }
        }

        NotifySessionsChanged();

        if (needsUsernameResolution)
        {
            var userId = claims.UserId!;
            LastBackgroundTask = Task.Run(async () =>
            {
                var resolved = await ResolveAndCacheUsernameAsync(sessionKey, userId, resolveUsernameAsync!);
                var displayUser = resolved ?? userId;

                if (shouldLogSessionStart)
                {
                    LogSessionStart(claims, displayUser, normalizedRole, normalizedFeature, safeIp);
                }

                if (!string.IsNullOrWhiteSpace(actionName) && actionOutcome != null)
                {
                    LogAction(claims, displayUser, actionName, actionOutcome);
                }
            });
        }
        else
        {
            var displayUser = claims.IsIdentityShared
                ? (cachedUsername ?? claims.UserId ?? "Unknown")
                : "Anonymous";

            if (shouldLogSessionStart)
            {
                LogSessionStart(claims, displayUser, normalizedRole, normalizedFeature, safeIp);
            }

            if (!string.IsNullOrWhiteSpace(actionName) && actionOutcome != null)
            {
                LogAction(claims, displayUser, actionName, actionOutcome);
            }
        }
    }

    public void RecordAuthFailure(TwitchExtensionTokenFailureReason reason)
    {
        if (reason == TwitchExtensionTokenFailureReason.None)
        {
            return;
        }

        var reasonLabel = reason switch
        {
            TwitchExtensionTokenFailureReason.MissingHeader => "missing_header",
            TwitchExtensionTokenFailureReason.UnconfiguredSecret => "unconfigured_secret",
            TwitchExtensionTokenFailureReason.Expired => "expired",
            TwitchExtensionTokenFailureReason.InvalidSignature => "invalid_signature",
            _ => "unknown"
        };

        AuthFailuresTotal.WithLabels(reasonLabel).Inc();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cleanupCts = new CancellationTokenSource();
        _cleanupTask = RunCleanupLoopAsync(_cleanupCts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cleanupCts != null)
        {
            await _cleanupCts.CancelAsync();
        }

        if (_cleanupTask != null)
        {
            try
            {
                await _cleanupTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }
    }

    internal void PruneExpiredSessions()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        bool changed;

        lock (_stateLock)
        {
            changed = PruneExpiredSessionsLocked(now);
            PruneExpiredCooldownsLocked(now);
        }

        if (changed)
        {
            NotifySessionsChanged();
        }
    }

    internal void UpdateGaugeMetrics()
    {
        try
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            bool pruned;

            lock (_metricsLock)
            {
                Dictionary<(string IdentityShared, string Role), int> activeByIdentityAndRole;
                Dictionary<string, int> activeByFeature;
                int uniqueSharedCount;
                int uniqueAnonymousCount;

                lock (_stateLock)
                {
                    pruned = PruneExpiredSessionsLocked(now);

                    activeByIdentityAndRole = _activeSessions.Values
                        .GroupBy(s => (IdentityShared: s.IsIdentityShared ? "true" : "false", Role: s.Role))
                        .ToDictionary(g => g.Key, g => g.Count());

                    activeByFeature = _activeSessions.Values
                        .GroupBy(s => s.LastFeature)
                        .ToDictionary(g => g.Key, g => g.Count());

                    uniqueSharedCount = _uniqueSharedUsers.Count;
                    uniqueAnonymousCount = _uniqueAnonymousUsers.Count;
                }

                foreach (var (label, count) in activeByIdentityAndRole)
                {
                    ActiveUsers.WithLabels(label.IdentityShared, label.Role).Set(count);
                    _publishedActiveUserLabels.Add(label);
                }

                foreach (var stale in _publishedActiveUserLabels)
                {
                    if (!activeByIdentityAndRole.ContainsKey(stale))
                    {
                        ActiveUsers.WithLabels(stale.IdentityShared, stale.Role).Set(0);
                    }
                }

                foreach (var (feature, count) in activeByFeature)
                {
                    ActiveUsersByFeature.WithLabels(feature).Set(count);
                    _publishedFeatureLabels.Add(feature);
                }

                foreach (var staleFeature in _publishedFeatureLabels)
                {
                    if (!activeByFeature.ContainsKey(staleFeature))
                    {
                        ActiveUsersByFeature.WithLabels(staleFeature).Set(0);
                    }
                }

                UniqueUsersSinceRestart.WithLabels("true").Set(uniqueSharedCount);
                UniqueUsersSinceRestart.WithLabels("false").Set(uniqueAnonymousCount);
            }

            if (pruned)
            {
                NotifySessionsChanged();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Twitch Extension Prometheus metrics");
        }
    }

    private async Task RunCleanupLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval, _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                PruneExpiredSessions();
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    private async Task<string?> ResolveAndCacheUsernameAsync(
        string sessionKey,
        string userId,
        Func<Task<string?>> resolveUsernameAsync)
    {
        try
        {
            var resolved = await resolveUsernameAsync();
            if (string.IsNullOrWhiteSpace(resolved))
            {
                return null;
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            _usernameCache[userId] = new CachedUsernameEntry(resolved, now);
            PruneUsernameCacheIfNeeded(now);
            var updated = false;

            lock (_stateLock)
            {
                if (_activeSessions.TryGetValue(sessionKey, out var session) &&
                    !string.Equals(session.Username, resolved, StringComparison.Ordinal))
                {
                    session.Username = resolved;
                    updated = true;
                }
            }

            if (updated)
            {
                NotifySessionsChanged();
            }

            return resolved;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve username for Twitch Extension user {UserId}", userId);
            return null;
        }
    }

    private void PruneUsernameCacheIfNeeded(DateTime nowUtc)
    {
        if (_usernameCache.Count <= MaxUsernameCacheEntries)
        {
            return;
        }

        var cutoffUtc = nowUtc - UsernameCacheTtl;
        foreach (var kvp in _usernameCache)
        {
            if (kvp.Value.CachedAtUtc < cutoffUtc)
            {
                _usernameCache.TryRemove(kvp);
            }
        }

        if (_usernameCache.Count > MaxUsernameCacheEntries)
        {
            var excess = _usernameCache.Count - MaxUsernameCacheEntries;
            foreach (var oldest in _usernameCache.OrderBy(kvp => kvp.Value.CachedAtUtc).Take(excess))
            {
                _usernameCache.TryRemove(oldest);
            }
        }
    }

    private void LogSessionStart(
        TwitchExtensionClaims claims,
        string displayUser,
        string role,
        string feature,
        string ip)
    {
        if (claims.IsIdentityShared && !string.IsNullOrWhiteSpace(claims.UserId))
        {
            _logger.LogInformation(
                "{Username} ({UserId}) started using the Twitch extension (Role: {Role}, Feature: {Feature}, IP: {Ip})",
                displayUser,
                claims.UserId,
                role,
                feature,
                ip);
        }
        else
        {
            _logger.LogInformation(
                "Anonymous viewer ({OpaqueUserId}) started using the Twitch extension (Role: {Role}, Feature: {Feature}, IP: {Ip})",
                claims.OpaqueUserId,
                role,
                feature,
                ip);
        }
    }

    private void LogAction(
        TwitchExtensionClaims claims,
        string displayUser,
        string actionName,
        string outcome)
    {
        if (claims.IsIdentityShared && !string.IsNullOrWhiteSpace(claims.UserId))
        {
            _logger.LogInformation(
                "{Username} ({UserId}) performed Twitch extension action {Action} ({Outcome})",
                displayUser,
                claims.UserId,
                actionName,
                outcome);
        }
        else
        {
            _logger.LogInformation(
                "Anonymous viewer ({OpaqueUserId}) performed Twitch extension action {Action} ({Outcome})",
                claims.OpaqueUserId,
                actionName,
                outcome);
        }
    }

    private bool PruneExpiredSessionsLocked(DateTime nowUtc)
    {
        List<TwitchExtensionSession>? expired = null;
        foreach (var session in _activeSessions.Values)
        {
            if ((nowUtc - session.LastSeenUtc) > _activeSessionTimeout)
            {
                expired ??= [];
                expired.Add(session);
            }
        }

        if (expired == null)
        {
            return false;
        }

        foreach (var session in expired)
        {
            _activeSessions.Remove(session.SessionKey);
            _logger.LogDebug(
                "Twitch extension session expired for {Username} ({SessionKey}) after {RequestCount} requests",
                session.Username,
                session.SessionKey,
                session.RequestCount);
        }

        return true;
    }

    private void PruneExpiredCooldownsLocked(DateTime nowUtc)
    {
        List<string>? staleKeys = null;
        foreach (var (key, lastLoggedUtc) in _lastLoggedActivityUtc)
        {
            if (!_activeSessions.ContainsKey(key) && (nowUtc - lastLoggedUtc) >= _reengagementLogCooldown)
            {
                staleKeys ??= [];
                staleKeys.Add(key);
            }
        }

        if (staleKeys == null)
        {
            return;
        }

        foreach (var key in staleKeys)
        {
            _lastLoggedActivityUtc.Remove(key);
        }
    }

    private void NotifySessionsChanged()
    {
        try
        {
            SessionsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error notifying Twitch Extension session subscribers");
        }
    }

    private static string GetSessionKey(TwitchExtensionClaims claims)
    {
        if (claims.IsIdentityShared && !string.IsNullOrWhiteSpace(claims.UserId))
        {
            return $"user:{claims.UserId}";
        }

        if (!string.IsNullOrWhiteSpace(claims.OpaqueUserId))
        {
            return $"opaque:{claims.OpaqueUserId}";
        }

        return string.Empty;
    }

    private static string NormalizeFeature(string? feature)
    {
        if (string.IsNullOrWhiteSpace(feature))
        {
            return "other";
        }

        var normalized = feature.Trim().ToLowerInvariant();
        return AllowedFeatures.Contains(normalized) ? normalized : "other";
    }

    private static string NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return "viewer";
        }

        var normalized = role.Trim().ToLowerInvariant();
        return AllowedRoles.Contains(normalized) ? normalized : "other";
    }

    private static string MapStatusCodeToOutcome(int statusCode)
    {
        return statusCode switch
        {
            >= 200 and < 300 => "success",
            401 or 403 => "unauthorized",
            >= 400 and < 500 => "client_error",
            _ => "server_error"
        };
    }

    private static TwitchExtensionSession CloneSession(TwitchExtensionSession source) => new()
    {
        SessionKey = source.SessionKey,
        UserId = source.UserId,
        OpaqueUserId = source.OpaqueUserId,
        Username = source.Username,
        Role = source.Role,
        IsIdentityShared = source.IsIdentityShared,
        UserIp = source.UserIp,
        LastFeature = source.LastFeature,
        LastEndpoint = source.LastEndpoint,
        FirstSeenUtc = source.FirstSeenUtc,
        LastSeenUtc = source.LastSeenUtc,
        RequestCount = source.RequestCount
    };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cleanupCts?.Cancel();
        _cleanupCts?.Dispose();
    }
}
