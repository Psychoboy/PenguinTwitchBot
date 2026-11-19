using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using PenguinTwitchBot.Controllers;
using PenguinTwitchBot.Services;
using Xunit;

namespace PenguinTwitchBot.Test.Services;

public sealed class TwitchExtensionTrackingServiceTests
{
    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (Entries)
            {
                Entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }

    private static TwitchExtensionClaims CreateSharedClaims(string userId = "12345", string role = "viewer") =>
        new(ChannelId: "chan1", UserId: userId, OpaqueUserId: $"U{userId}", Role: role, IsIdentityShared: true);

    private static TwitchExtensionClaims CreateAnonymousClaims(string opaqueId = "Aanon999", string role = "viewer") =>
        new(ChannelId: "chan1", UserId: null, OpaqueUserId: opaqueId, Role: role, IsIdentityShared: false);

    [Fact]
    public async Task RecordRequest_TracksSharedAndAnonymousSessions_AndResolvesUsername()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));
        var logger = new ListLogger<TwitchExtensionTrackingService>();
        using var service = new TwitchExtensionTrackingService(logger, timeProvider: time);

        var changedCount = 0;
        service.SessionsChanged += (_, _) => changedCount++;

        service.RecordRequest(
            CreateSharedClaims("u100", "moderator"),
            feature: "fishing",
            endpoint: "fishing_viewer",
            httpMethod: "GET",
            statusCode: 200,
            clientIp: "192.168.1.10",
            resolveUsernameAsync: () => Task.FromResult<string?>("coolviewer"));

        if (service.LastBackgroundTask != null)
        {
            await service.LastBackgroundTask;
        }

        service.RecordRequest(
            CreateAnonymousClaims("Aanon777"),
            feature: "leaderboards",
            endpoint: "leaderboards",
            httpMethod: "GET",
            statusCode: 200,
            clientIp: "192.168.1.20");

        var sessions = service.GetActiveSessions();
        Assert.Equal(2, sessions.Count);
        Assert.True(changedCount >= 2);

        var shared = Assert.Single(sessions, s => s.IsIdentityShared);
        Assert.Equal("coolviewer", shared.Username);
        Assert.Equal("u100", shared.UserId);
        Assert.Equal("moderator", shared.Role);
        Assert.Equal("fishing", shared.LastFeature);
        Assert.Equal("192.168.1.10", shared.UserIp);

        var anon = Assert.Single(sessions, s => !s.IsIdentityShared);
        Assert.Equal("Anonymous", anon.Username);
        Assert.Equal("Aanon777", anon.OpaqueUserId);
        Assert.Equal("leaderboards", anon.LastFeature);
    }

    [Fact]
    public void GetActiveSessions_ExpiresInactiveSessions_AfterTimeout()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));
        var logger = new ListLogger<TwitchExtensionTrackingService>();
        using var service = new TwitchExtensionTrackingService(logger, timeProvider: time);

        service.RecordRequest(
            CreateSharedClaims("u200"),
            feature: "giveaway",
            endpoint: "giveaway",
            httpMethod: "GET",
            statusCode: 200,
            clientIp: "10.0.0.1");

        Assert.Single(service.GetActiveSessions());

        // Still active at 2 minutes
        time.Advance(TimeSpan.FromMinutes(2));
        Assert.Single(service.GetActiveSessions());

        // Expired past 3 minutes
        time.Advance(TimeSpan.FromMinutes(2));
        Assert.Empty(service.GetActiveSessions());
    }

    [Fact]
    public async Task RecordRequest_ThrottlesSessionStartLogs_UntilCooldownElapses()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));
        var logger = new ListLogger<TwitchExtensionTrackingService>();
        using var service = new TwitchExtensionTrackingService(logger, timeProvider: time);

        var claims = CreateSharedClaims("u300");

        // Initial request -> logs once
        service.RecordRequest(
            claims,
            feature: "features",
            endpoint: "features",
            httpMethod: "GET",
            statusCode: 200,
            clientIp: "10.0.0.2",
            resolveUsernameAsync: () => Task.FromResult<string?>("penguinuser"));
        if (service.LastBackgroundTask != null)
        {
            await service.LastBackgroundTask;
        }

        // Routine 30s refreshes over 10 minutes -> no additional logs
        for (var i = 0; i < 10; i++)
        {
            time.Advance(TimeSpan.FromSeconds(30));
            service.RecordRequest(
                claims,
                feature: "fishing",
                endpoint: "fishing_viewer",
                httpMethod: "GET",
                statusCode: 200,
                clientIp: "10.0.0.2");
        }

        var startLogsBeforeCooldown = logger.Entries.Count(e =>
            e.Level == LogLevel.Information && e.Message.Contains("started using the Twitch extension"));
        Assert.Equal(1, startLogsBeforeCooldown);
        Assert.Contains(logger.Entries, e => e.Message.Contains("penguinuser (u300) started using the Twitch extension"));

        // Advance past the 15-minute cooldown -> logs again
        time.Advance(TimeSpan.FromMinutes(15));
        service.RecordRequest(
            claims,
            feature: "giveaway",
            endpoint: "giveaway",
            httpMethod: "GET",
            statusCode: 200,
            clientIp: "10.0.0.2");

        var startLogsAfterCooldown = logger.Entries.Count(e =>
            e.Level == LogLevel.Information && e.Message.Contains("started using the Twitch extension"));
        Assert.Equal(2, startLogsAfterCooldown);
    }

    [Fact]
    public void RecordRequest_LogsInteractiveActions_AndUpdatesPrometheusMetrics()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));
        var logger = new ListLogger<TwitchExtensionTrackingService>();
        using var service = new TwitchExtensionTrackingService(logger, timeProvider: time);

        var beforeRequests = TwitchExtensionTrackingService.RequestsTotal
            .WithLabels("giveaway", "giveaway_enter", "POST", "200", "true").Value;
        var beforeActions = TwitchExtensionTrackingService.ActionsTotal
            .WithLabels("giveaway_enter", "success").Value;
        var beforeAuthFailures = TwitchExtensionTrackingService.AuthFailuresTotal
            .WithLabels("expired").Value;

        service.RecordRequest(
            CreateSharedClaims("u400"),
            feature: "giveaway",
            endpoint: "giveaway_enter",
            httpMethod: "POST",
            statusCode: 200,
            clientIp: "10.0.0.4",
            actionName: "giveaway_enter");

        service.RecordAuthFailure(TwitchExtensionTokenFailureReason.Expired);
        service.UpdateGaugeMetrics();

        Assert.Equal(beforeRequests + 1, TwitchExtensionTrackingService.RequestsTotal
            .WithLabels("giveaway", "giveaway_enter", "POST", "200", "true").Value);
        Assert.Equal(beforeActions + 1, TwitchExtensionTrackingService.ActionsTotal
            .WithLabels("giveaway_enter", "success").Value);
        Assert.Equal(beforeAuthFailures + 1, TwitchExtensionTrackingService.AuthFailuresTotal
            .WithLabels("expired").Value);
        Assert.Equal(1, TwitchExtensionTrackingService.ActiveUsers.WithLabels("true", "viewer").Value);
        Assert.Equal(1, TwitchExtensionTrackingService.ActiveUsersByFeature.WithLabels("giveaway").Value);
        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Information && e.Message.Contains("performed Twitch extension action giveaway_enter (success)"));

        // After session expires, active gauges drop back to 0
        time.Advance(TimeSpan.FromMinutes(5));
        service.UpdateGaugeMetrics();

        Assert.Equal(0, TwitchExtensionTrackingService.ActiveUsers.WithLabels("true", "viewer").Value);
        Assert.Equal(0, TwitchExtensionTrackingService.ActiveUsersByFeature.WithLabels("giveaway").Value);
    }

    [Fact]
    public void RecordRequest_ReplacesExpiredSession_AndTracksSessionsStartedIndependentlyOfLogCooldown()
    {
        var startTime = new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(startTime);
        var logger = new ListLogger<TwitchExtensionTrackingService>();
        using var service = new TwitchExtensionTrackingService(logger, timeProvider: time);

        var claims = CreateSharedClaims("u500");
        var beforeSessionsStarted = TwitchExtensionTrackingService.SessionsStartedTotal.WithLabels("true").Value;

        // First request starts session 1
        service.RecordRequest(claims, "features", "features", "GET", 200, "10.0.0.5");
        Assert.Equal(beforeSessionsStarted + 1, TwitchExtensionTrackingService.SessionsStartedTotal.WithLabels("true").Value);

        // Stay continuously active past the 15-minute log cooldown (every 2 minutes) -> still same session
        for (var i = 0; i < 9; i++)
        {
            time.Advance(TimeSpan.FromMinutes(2));
            service.RecordRequest(claims, "fishing", "fishing_viewer", "GET", 200, "10.0.0.5");
        }

        Assert.Equal(beforeSessionsStarted + 1, TwitchExtensionTrackingService.SessionsStartedTotal.WithLabels("true").Value);
        var active = Assert.Single(service.GetActiveSessions());
        Assert.Equal(startTime.UtcDateTime, active.FirstSeenUtc);
        Assert.Equal(10, active.RequestCount);

        // Go inactive past the 3-minute session timeout (without calling PruneExpiredSessions) -> starts session 2
        time.Advance(TimeSpan.FromMinutes(4));
        var resumeTime = time.GetUtcNow().UtcDateTime;
        service.RecordRequest(claims, "giveaway", "giveaway", "GET", 200, "10.0.0.5");

        Assert.Equal(beforeSessionsStarted + 2, TwitchExtensionTrackingService.SessionsStartedTotal.WithLabels("true").Value);
        var resumed = Assert.Single(service.GetActiveSessions());
        Assert.Equal(resumeTime, resumed.FirstSeenUtc);
        Assert.Equal(1, resumed.RequestCount);
    }
}
