using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.TwitchApi.Models.Subscriptions;
using Xunit;

#pragma warning disable NS1000, NS1001, NS1004, NS5000

namespace PenguinTwitchBot.Test.Bot.Core;

public class SubscriptionTrackerTests
{
    private readonly ILogger<SubscriptionTracker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SubscriptionTracker _tracker;

    public SubscriptionTrackerTests()
    {
        _logger = Substitute.For<ILogger<SubscriptionTracker>>();
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _tracker = new SubscriptionTracker(_logger, _scopeFactory);
    }

    [Fact]
    public async Task ExistingSub_WhenUserExists_ReturnsTrue()
    {
        var subHistory = new List<SubscriptionHistory>
        {
            new() { Username = "testuser", LastSub = DateTime.UtcNow }
        }.BuildMockDbSet().AsQueryable();

        var mockDb = Substitute.For<IUnitOfWork>();
        var mockRepo = Substitute.For<ISubscriptionHistoriesRepository>();
        mockRepo.Find(Arg.Any<System.Linq.Expressions.Expression<System.Func<SubscriptionHistory, bool>>>()).ReturnsForAnyArgs(subHistory);
        mockDb.SubscriptionHistories.Returns(mockRepo);

        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(mockDb);
        scope.ServiceProvider.Returns(serviceProvider);
        _scopeFactory.CreateAsyncScope().Returns(scope);

        bool result = await _tracker.ExistingSub("testuser");

        Assert.True(result);
    }

    [Fact]
    public async Task ExistingSub_WhenUserDoesNotExist_ReturnsFalse()
    {
        var subHistory = new List<SubscriptionHistory>().BuildMockDbSet().AsQueryable();

        var mockDb = Substitute.For<IUnitOfWork>();
        var mockRepo = Substitute.For<ISubscriptionHistoriesRepository>();
        mockRepo.Find(Arg.Any<System.Linq.Expressions.Expression<System.Func<SubscriptionHistory, bool>>>()).ReturnsForAnyArgs(subHistory);
        mockDb.SubscriptionHistories.Returns(mockRepo);

        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(mockDb);
        scope.ServiceProvider.Returns(serviceProvider);
        _scopeFactory.CreateAsyncScope().Returns(scope);

        bool result = await _tracker.ExistingSub("nonexistent");

        Assert.False(result);
    }

    [Fact]
    public async Task ExistingSub_WhenRepositoryThrows_ReturnsFalse()
    {
        var mockDb = Substitute.For<IUnitOfWork>();
        var mockRepo = Substitute.For<ISubscriptionHistoriesRepository>();
        mockRepo.Find(Arg.Any<System.Linq.Expressions.Expression<System.Func<SubscriptionHistory, bool>>>()).ReturnsForAnyArgs(x => throw new InvalidOperationException("DB error"));
        mockDb.SubscriptionHistories.Returns(mockRepo);

        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(mockDb);
        scope.ServiceProvider.Returns(serviceProvider);
        _scopeFactory.CreateAsyncScope().Returns(scope);

        bool result = await _tracker.ExistingSub("testuser");

        Assert.False(result);
        _logger.Received(1).LogError(Arg.Any<Exception>(), "Error checking existing sub");
    }

    [Fact]
    public async Task LastSub_WhenUserExists_ReturnsLastSubDate()
    {
        var expectedDate = DateTime.UtcNow.AddDays(-1);
        var subHistory = new List<SubscriptionHistory>
        {
            new() { Username = "testuser", LastSub = expectedDate }
        }.BuildMockDbSet().AsQueryable();

        var mockDb = Substitute.For<IUnitOfWork>();
        var mockRepo = Substitute.For<ISubscriptionHistoriesRepository>();
        mockRepo.Find(Arg.Any<System.Linq.Expressions.Expression<System.Func<SubscriptionHistory, bool>>>()).ReturnsForAnyArgs(subHistory);
        mockDb.SubscriptionHistories.Returns(mockRepo);

        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(mockDb);
        scope.ServiceProvider.Returns(serviceProvider);
        _scopeFactory.CreateAsyncScope().Returns(scope);

        DateTime? result = await _tracker.LastSub("testuser");

        Assert.Equal(expectedDate, result);
    }

    [Fact]
    public async Task LastSub_WhenUserDoesNotExist_ReturnsNull()
    {
        var subHistory = new List<SubscriptionHistory>().BuildMockDbSet().AsQueryable();

        var mockDb = Substitute.For<IUnitOfWork>();
        var mockRepo = Substitute.For<ISubscriptionHistoriesRepository>();
        mockRepo.Find(Arg.Any<System.Linq.Expressions.Expression<System.Func<SubscriptionHistory, bool>>>()).ReturnsForAnyArgs(subHistory);
        mockDb.SubscriptionHistories.Returns(mockRepo);

        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(mockDb);
        scope.ServiceProvider.Returns(serviceProvider);
        _scopeFactory.CreateAsyncScope().Returns(scope);

        DateTime? result = await _tracker.LastSub("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task MissingSubs_ReturnsOnlyMissingNames()
    {
        var subHistory = new List<SubscriptionHistory>
        {
            new() { Username = "user1", LastSub = DateTime.UtcNow },
            new() { Username = "user3", LastSub = DateTime.UtcNow }
        }.BuildMockDbSet().AsQueryable();

        var mockDb = Substitute.For<IUnitOfWork>();
        var mockRepo = Substitute.For<ISubscriptionHistoriesRepository>();
        mockRepo.Find(Arg.Any<System.Linq.Expressions.Expression<System.Func<SubscriptionHistory, bool>>>()).ReturnsForAnyArgs(subHistory);
        mockDb.SubscriptionHistories.Returns(mockRepo);

        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(mockDb);
        scope.ServiceProvider.Returns(serviceProvider);
        _scopeFactory.CreateAsyncScope().Returns(scope);

        var result = await _tracker.MissingSubs(new[] { "user1", "user2", "user3" });

        Assert.Equal(new[] { "user2" }, result);
    }

    [Fact]
    public async Task MissingSubs_WhenRepositoryThrows_ReturnsEmptyList()
    {
        var mockDb = Substitute.For<IUnitOfWork>();
        var mockRepo = Substitute.For<ISubscriptionHistoriesRepository>();
        mockRepo.Find(Arg.Any<System.Linq.Expressions.Expression<System.Func<SubscriptionHistory, bool>>>()).ReturnsForAnyArgs(x => throw new InvalidOperationException("DB error"));
        mockDb.SubscriptionHistories.Returns(mockRepo);

        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(mockDb);
        scope.ServiceProvider.Returns(serviceProvider);
        _scopeFactory.CreateAsyncScope().Returns(scope);

        var result = await _tracker.MissingSubs(new[] { "user1", "user2" });

        Assert.Empty(result);
        _logger.Received(1).LogError(Arg.Any<Exception>(), "Error checking missing subs");
    }

    [Fact]
    public async Task AddOrUpdateSubHistory_WhenUserDoesNotExist_CreatesNew()
    {
        var subHistory = new List<SubscriptionHistory>().BuildMockDbSet().AsQueryable();
        var mockDb = Substitute.For<IUnitOfWork>();
        var mockRepo = Substitute.For<ISubscriptionHistoriesRepository>();
        mockRepo.Find(Arg.Any<System.Linq.Expressions.Expression<System.Func<SubscriptionHistory, bool>>>()).ReturnsForAnyArgs(subHistory);
        mockDb.SubscriptionHistories.Returns(mockRepo);
        mockDb.SaveChangesAsync().Returns(1);

        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(mockDb);
        scope.ServiceProvider.Returns(serviceProvider);
        _scopeFactory.CreateAsyncScope().Returns(scope);

        await _tracker.AddOrUpdateSubHistory("newuser", "uid-123");

        mockRepo.Received(1).Update(Arg.Is<SubscriptionHistory>(s => s.Username == "newuser" && s.UserId == "uid-123"));
        await mockDb.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task AddOrUpdateSubHistory_WhenUserExists_UpdatesExisting()
    {
        var existing = new SubscriptionHistory { Username = "testuser", UserId = "uid-old", LastSub = DateTime.UtcNow.AddDays(-1) };
        var subHistory = new List<SubscriptionHistory> { existing }.BuildMockDbSet().AsQueryable();

        var mockDb = Substitute.For<IUnitOfWork>();
        var mockRepo = Substitute.For<ISubscriptionHistoriesRepository>();
        mockRepo.Find(Arg.Any<System.Linq.Expressions.Expression<System.Func<SubscriptionHistory, bool>>>()).ReturnsForAnyArgs(subHistory);
        mockDb.SubscriptionHistories.Returns(mockRepo);
        mockDb.SaveChangesAsync().Returns(1);

        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(mockDb);
        scope.ServiceProvider.Returns(serviceProvider);
        _scopeFactory.CreateAsyncScope().Returns(scope);

        await _tracker.AddOrUpdateSubHistory("testuser", "uid-new");

        mockRepo.Received(1).Update(Arg.Is<SubscriptionHistory>(s => s.Username == "testuser" && s.UserId == "uid-old"));
        await mockDb.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task AddOrUpdateSubHistory_WhenRepositoryThrows_LogsError()
    {
        var mockDb = Substitute.For<IUnitOfWork>();
        var mockRepo = Substitute.For<ISubscriptionHistoriesRepository>();
        mockRepo.Find(Arg.Any<System.Linq.Expressions.Expression<System.Func<SubscriptionHistory, bool>>>()).ReturnsForAnyArgs(x => throw new InvalidOperationException("DB error"));
        mockDb.SubscriptionHistories.Returns(mockRepo);

        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(mockDb);
        scope.ServiceProvider.Returns(serviceProvider);
        _scopeFactory.CreateAsyncScope().Returns(scope);

        await _tracker.AddOrUpdateSubHistory("testuser", "uid-123");

        _logger.Received(1).LogError(Arg.Any<Exception>(), "Error Adding or Updating sub history");
    }

    [Fact]
    public async Task RecordSubscriptionAsync_NewSub_CreatesActiveRecordWithDefaultOneMonth()
    {
        var histories = new List<SubscriptionHistory>();
        var (mockDb, mockSubRepo, _) = CreateScopedDb(histories, []);

        await _tracker.RecordSubscriptionAsync(
            userId: "u1",
            username: "Alice",
            displayName: "Alice",
            tier: "1000",
            isGift: false);

        await mockSubRepo.Received(1).AddAsync(Arg.Is<SubscriptionHistory>(s =>
            s.UserId == "u1" &&
            s.Username == "alice" &&
            s.DisplayName == "Alice" &&
            s.IsActive &&
            s.Tier == "1000" &&
            s.TotalMonths == 1 &&
            s.ConsecutiveMonths == 1 &&
            s.EndedAtUtc == null));
        await mockDb.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task RecordSubscriptionAsync_RenewalWithNullStreak_PreservesExistingStreak()
    {
        var existing = new SubscriptionHistory
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            Username = "alice",
            DisplayName = "Alice",
            IsActive = true,
            TotalMonths = 10,
            ConsecutiveMonths = 6
        };
        var (mockDb, mockSubRepo, _) = CreateScopedDb([existing], []);

        await _tracker.RecordSubscriptionAsync(
            userId: "u1",
            username: "alice",
            displayName: "Alice",
            tier: "2000",
            isGift: false,
            totalMonths: 11,
            consecutiveMonths: null,
            isRenewal: true);

        Assert.Equal(11, existing.TotalMonths);
        Assert.Equal(6, existing.ConsecutiveMonths);
        Assert.Equal("2000", existing.Tier);
        Assert.True(existing.IsActive);
        mockSubRepo.Received(1).Update(existing);
        await mockDb.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task RecordChatSubscriberBadgeMonthsAsync_UpdatesHigherMonthsAndCachesRepeatedValues()
    {
        var existing = new SubscriptionHistory
        {
            Id = Guid.NewGuid(),
            UserId = "u42",
            Username = "bob",
            DisplayName = "Bob",
            IsActive = true,
            TotalMonths = 5
        };
        var (mockDb, mockSubRepo, _) = CreateScopedDb([existing], []);

        await _tracker.RecordChatSubscriberBadgeMonthsAsync("u42", "bob", "Bob", 14);
        // Second call with same or lower months should short-circuit via cache without DB hit
        await _tracker.RecordChatSubscriberBadgeMonthsAsync("u42", "bob", "Bob", 14);
        await _tracker.RecordChatSubscriberBadgeMonthsAsync("u42", "bob", "Bob", 12);

        Assert.Equal(14, existing.TotalMonths);
        mockSubRepo.Received(1).Update(existing);
        await mockDb.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task RecordSubscriptionEndAsync_MarksPastAndResetsConsecutivePreservingTotal()
    {
        var existing = new SubscriptionHistory
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            Username = "alice",
            IsActive = true,
            TotalMonths = 18,
            ConsecutiveMonths = 12
        };
        var (mockDb, mockSubRepo, _) = CreateScopedDb([existing], []);

        await _tracker.RecordSubscriptionEndAsync("u1", "alice");

        Assert.False(existing.IsActive);
        Assert.Equal(0, existing.ConsecutiveMonths);
        Assert.Equal(18, existing.TotalMonths);
        Assert.NotNull(existing.EndedAtUtc);
        mockSubRepo.Received(1).Update(existing);
        await mockDb.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task SyncSubscribersFromTwitchAsync_AddsActiveUpdatesExistingAndMarksMissingAsPast()
    {
        var twitchService = Substitute.For<ITwitchService>();
        twitchService.GetBroadcasterSubscriptionsSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new BroadcasterSubscriptionsSnapshot(
                Subscriptions:
                [
                    new Subscription("u1", "alice", "Alice", Tier: "2000", IsGift: false),
                    new Subscription("u2", "charlie", "Charlie", Tier: "1000", IsGift: true, GifterName: "GenerousUser")
                ],
                Total: 2,
                Points: 3));

        var existingAlice = new SubscriptionHistory
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            Username = "alice",
            DisplayName = "Alice",
            IsActive = true,
            Tier = "1000",
            TotalMonths = 8,
            ConsecutiveMonths = 8
        };
        var lapsedBob = new SubscriptionHistory
        {
            Id = Guid.NewGuid(),
            UserId = "u3",
            Username = "bob",
            DisplayName = "Bob",
            IsActive = true,
            Tier = "1000",
            TotalMonths = 5,
            ConsecutiveMonths = 5
        };
        var bobViewer = new Viewer { Id = Guid.NewGuid(), UserId = "u3", Username = "bob", DisplayName = "Bob", isSub = true };
        var aliceViewer = new Viewer { Id = Guid.NewGuid(), UserId = "u1", Username = "alice", DisplayName = "Alice", isSub = false };

        var (mockDb, mockSubRepo, mockViewerRepo) = CreateScopedDb([existingAlice, lapsedBob], [aliceViewer, bobViewer]);
        var tracker = new SubscriptionTracker(_logger, _scopeFactory, twitchService);

        var result = await tracker.SyncSubscribersFromTwitchAsync();

        Assert.Equal(2, result.TotalTwitchSubscribers);
        Assert.Equal(3, result.SubPoints);
        Assert.Equal(1, result.AddedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.MarkedPastCount);

        Assert.Equal("2000", existingAlice.Tier);
        Assert.True(existingAlice.IsActive);
        Assert.True(aliceViewer.isSub);

        Assert.False(lapsedBob.IsActive);
        Assert.Equal(0, lapsedBob.ConsecutiveMonths);
        Assert.Equal(5, lapsedBob.TotalMonths);
        Assert.NotNull(lapsedBob.EndedAtUtc);
        Assert.False(bobViewer.isSub);

        await mockSubRepo.Received(1).AddAsync(Arg.Is<SubscriptionHistory>(s =>
            s.UserId == "u2" && s.IsActive && s.IsGift && s.GifterName == "GenerousUser"));
        await mockViewerRepo.Received(1).AddAsync(Arg.Is<Viewer>(v => v.UserId == "u2" && v.isSub));
        await mockDb.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task GetTopCurrentSubscribersAsync_ReturnsTop10ActiveSortedByMetric()
    {
        var now = DateTime.UtcNow;
        var rows = new List<SubscriptionHistory>
        {
            // Inactive past sub with huge months must be excluded
            new() { Id = Guid.NewGuid(), UserId = "past", Username = "pastking", DisplayName = "PastKing", IsActive = false, TotalMonths = 99, ConsecutiveMonths = 99, LastSub = now.AddDays(-5) },
            // Active sub with 0 months (unknown) must be excluded
            new() { Id = Guid.NewGuid(), UserId = "zero", Username = "unknown", DisplayName = "Unknown", IsActive = true, TotalMonths = 0, ConsecutiveMonths = 0, LastSub = now },
            new() { Id = Guid.NewGuid(), UserId = "u1", Username = "veteran", DisplayName = "Veteran", IsActive = true, TotalMonths = 24, ConsecutiveMonths = 3, Tier = "1000", LastSub = now },
            new() { Id = Guid.NewGuid(), UserId = "u2", Username = "streaker", DisplayName = "Streaker", IsActive = true, TotalMonths = 15, ConsecutiveMonths = 15, Tier = "3000", LastSub = now },
        };

        CreateScopedDb(rows, []);

        var byTotal = await _tracker.GetTopCurrentSubscribersAsync(SubscriberLeaderboardMetric.TotalMonths, limit: 10);
        Assert.Equal(2, byTotal.Count);
        Assert.Equal("Veteran", byTotal[0].DisplayName);
        Assert.Equal(1, byTotal[0].Rank);
        Assert.Equal("Streaker", byTotal[1].DisplayName);
        Assert.Equal(2, byTotal[1].Rank);

        var byStreak = await _tracker.GetTopCurrentSubscribersAsync(SubscriberLeaderboardMetric.ConsecutiveMonths, limit: 10);
        Assert.Equal(2, byStreak.Count);
        Assert.Equal("Streaker", byStreak[0].DisplayName);
        Assert.Equal(15, byStreak[0].ConsecutiveMonths);
        Assert.Equal("Veteran", byStreak[1].DisplayName);
        Assert.Equal(3, byStreak[1].ConsecutiveMonths);
    }

    [Fact]
    public async Task UpdateSubscriberMonthsAsync_ClampsTotalToAtLeastConsecutive()
    {
        var id = Guid.NewGuid();
        var existing = new SubscriptionHistory
        {
            Id = id,
            UserId = "u1",
            Username = "alice",
            IsActive = true,
            TotalMonths = 1,
            ConsecutiveMonths = 1
        };
        var (mockDb, mockSubRepo, _) = CreateScopedDb([existing], []);

        await _tracker.UpdateSubscriberMonthsAsync(id, totalMonths: 4, consecutiveMonths: 7);

        Assert.Equal(7, existing.TotalMonths);
        Assert.Equal(7, existing.ConsecutiveMonths);
        mockSubRepo.Received(1).Update(existing);
        await mockDb.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task SyncSubscribersFromTwitchAsync_DeduplicatesLegacyRowsAndUsesSyncedCount()
    {
        var twitchService = Substitute.For<ITwitchService>();
        twitchService.GetBroadcasterSubscriptionsSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new BroadcasterSubscriptionsSnapshot(
                Subscriptions:
                [
                    new Subscription("u1", "alice", "Alice", Tier: "1000", IsGift: false)
                ],
                Total: 5, // Simulate Twitch total diverging from actual returned items
                Points: 5));

        var primaryAlice = new SubscriptionHistory
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            Username = "alice",
            IsActive = true,
            TotalMonths = 4,
            ConsecutiveMonths = 2
        };
        var duplicateAlice = new SubscriptionHistory
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            Username = "alice",
            IsActive = true,
            TotalMonths = 10,
            ConsecutiveMonths = 6
        };

        var (mockDb, mockSubRepo, _) = CreateScopedDb([primaryAlice, duplicateAlice], []);
        var tracker = new SubscriptionTracker(_logger, _scopeFactory, twitchService);

        var result = await tracker.SyncSubscribersFromTwitchAsync();

        Assert.Equal(1, result.TotalTwitchSubscribers);
        Assert.Equal(10, primaryAlice.TotalMonths);
        Assert.Equal(6, primaryAlice.ConsecutiveMonths);
        mockSubRepo.Received(1).Remove(duplicateAlice);
        await mockDb.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task SyncAndLeaderboard_ExcludesBroadcasterAndBotAccounts()
    {
        var twitchService = Substitute.For<ITwitchService>();
        twitchService.GetBroadcasterUserId().Returns("broadcaster-id");
        twitchService.GetBotUserId().Returns("bot-id");
        twitchService.GetBroadcasterSubscriptionsSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new BroadcasterSubscriptionsSnapshot(
                Subscriptions:
                [
                    new Subscription("broadcaster-id", "mystreamer", "MyStreamer", Tier: "3000"),
                    new Subscription("bot-id", "mybot", "MyBot", Tier: "1000"),
                    new Subscription("u1", "alice", "Alice", Tier: "1000")
                ],
                Total: 3,
                Points: 5));

        var serviceBackbone = Substitute.For<IServiceBackbone>();
        serviceBackbone.BroadcasterName.Returns("MyStreamer");
        serviceBackbone.BotName.Returns("MyBot");
        serviceBackbone.IsBroadcasterOrBot(Arg.Any<string>())
            .Returns(call =>
            {
                var n = call.Arg<string>();
                return string.Equals(n, "mystreamer", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(n, "mybot", StringComparison.OrdinalIgnoreCase);
            });

        var existingStreamerRow = new SubscriptionHistory { Id = Guid.NewGuid(), UserId = "broadcaster-id", Username = "mystreamer", IsActive = true, TotalMonths = 60, ConsecutiveMonths = 60 };
        var existingBotRow = new SubscriptionHistory { Id = Guid.NewGuid(), UserId = "bot-id", Username = "mybot", IsActive = true, TotalMonths = 24, ConsecutiveMonths = 24 };
        var existingAliceRow = new SubscriptionHistory { Id = Guid.NewGuid(), UserId = "u1", Username = "alice", DisplayName = "Alice", IsActive = true, TotalMonths = 12, ConsecutiveMonths = 12 };

        var (_, mockSubRepo, _) = CreateScopedDb([existingStreamerRow, existingBotRow, existingAliceRow], []);
        var tracker = new SubscriptionTracker(_logger, _scopeFactory, twitchService, serviceBackbone);

        var syncResult = await tracker.SyncSubscribersFromTwitchAsync();
        Assert.Equal(1, syncResult.TotalTwitchSubscribers);
        mockSubRepo.Received(1).Remove(existingStreamerRow);
        mockSubRepo.Received(1).Remove(existingBotRow);

        var top = await tracker.GetTopCurrentSubscribersAsync(SubscriberLeaderboardMetric.TotalMonths, limit: 10);
        var entry = Assert.Single(top);
        Assert.Equal("Alice", entry.DisplayName);

        var page = await tracker.GetSubscribersPageAsync(0, 25, SubscriberFilter.Current);
        Assert.Equal(1, page.CurrentCount);
        Assert.Single(page.Items);
        Assert.Equal("alice", page.Items[0].Username);
    }

    private (IUnitOfWork Db, ISubscriptionHistoriesRepository SubRepo, IViewersRepository ViewerRepo) CreateScopedDb(
        List<SubscriptionHistory> histories,
        List<Viewer> viewers)
    {
        var mockDb = Substitute.For<IUnitOfWork>();
        var mockSubRepo = Substitute.For<ISubscriptionHistoriesRepository>();
        var mockViewerRepo = Substitute.For<IViewersRepository>();

        mockSubRepo.Find(Arg.Any<System.Linq.Expressions.Expression<Func<SubscriptionHistory, bool>>>())
            .Returns(callInfo =>
            {
                var predicate = callInfo.Arg<System.Linq.Expressions.Expression<Func<SubscriptionHistory, bool>>>().Compile();
                return histories.Where(predicate).ToList().BuildMockDbSet().AsQueryable();
            });

        mockViewerRepo.Find(Arg.Any<System.Linq.Expressions.Expression<Func<Viewer, bool>>>())
            .Returns(callInfo =>
            {
                var predicate = callInfo.Arg<System.Linq.Expressions.Expression<Func<Viewer, bool>>>().Compile();
                return viewers.Where(predicate).ToList().BuildMockDbSet().AsQueryable();
            });

        mockDb.SubscriptionHistories.Returns(mockSubRepo);
        mockDb.Viewers.Returns(mockViewerRepo);
        mockDb.SaveChangesAsync().Returns(1);

        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(mockDb);
        scope.ServiceProvider.Returns(serviceProvider);
        _scopeFactory.CreateAsyncScope().Returns(scope);

        return (mockDb, mockSubRepo, mockViewerRepo);
    }
}

#pragma warning restore NS1000, NS1001, NS1004, NS5000
