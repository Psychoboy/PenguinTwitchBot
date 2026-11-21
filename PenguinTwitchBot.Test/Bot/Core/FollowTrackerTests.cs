using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Core;

public sealed class FollowTrackerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _dbOptions;
    private readonly ILogger<FollowTracker> _logger;
    private readonly ITwitchService _twitchService;
    private readonly IServiceProvider _serviceProvider;
    private readonly FollowTracker _tracker;

    public FollowTrackerTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using (var initContext = new ApplicationDbContext(_dbOptions))
        {
            initContext.Database.EnsureCreated();
        }

        _logger = Substitute.For<ILogger<FollowTracker>>();
        _twitchService = Substitute.For<ITwitchService>();

        var services = new ServiceCollection();
        services.AddScoped(_ => new ApplicationDbContext(_dbOptions));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        _serviceProvider = services.BuildServiceProvider();

        _tracker = new FollowTracker(
            _logger,
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _twitchService);
    }

    public void Dispose()
    {
        (_serviceProvider as IDisposable)?.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task TryBeginAndCompleteFollowDispatch_FirstFollow_PersistsAndMarksDispatched()
    {
        var followDate = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

        var began = await _tracker.TryBeginFollowDispatchAsync("uid-1", "TestUser", "TestUser", followDate);

        Assert.True(began);
        Assert.Equal(1, await _tracker.GetPersistedFollowerCountAsync());

        // Duplicate concurrent delivery while in-flight is suppressed
        Assert.False(await _tracker.TryBeginFollowDispatchAsync("uid-1", "TestUser", "TestUser", followDate));

        await _tracker.CompleteFollowDispatchAsync("uid-1");

        await using var verifyContext = new ApplicationDbContext(_dbOptions);
        var saved = await verifyContext.Followers.SingleAsync();
        Assert.Equal("uid-1", saved.UserId);
        Assert.Equal("testuser", saved.Username);
        Assert.Equal("TestUser", saved.DisplayName);
        Assert.Equal(followDate, saved.FollowDate);
        Assert.True(saved.FollowEventDispatched);
    }

    [Fact]
    public async Task TryBeginFollowDispatchAsync_RefollowAfterComplete_ReturnsFalsePreservesDateAndUpdatesName()
    {
        var originalDate = new DateTime(2025, 1, 15, 8, 30, 0, DateTimeKind.Utc);
        var refollowDate = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(await _tracker.TryBeginFollowDispatchAsync("uid-1", "oldname", "OldName", originalDate));
        await _tracker.CompleteFollowDispatchAsync("uid-1");

        var isNewOnRefollow = await _tracker.TryBeginFollowDispatchAsync("uid-1", "NewName", "NewName", refollowDate);

        Assert.False(isNewOnRefollow);
        Assert.Equal(1, await _tracker.GetPersistedFollowerCountAsync());

        await using var verifyContext = new ApplicationDbContext(_dbOptions);
        var saved = await verifyContext.Followers.SingleAsync();
        Assert.Equal("newname", saved.Username);
        Assert.Equal("NewName", saved.DisplayName);
        Assert.Equal(originalDate, saved.FollowDate);
    }

    [Fact]
    public async Task ReleaseFollowDispatch_AllowsRetryForSameFollowButSuppressesLaterRefollow()
    {
        var followDate = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(await _tracker.TryBeginFollowDispatchAsync("uid-1", "testuser", "TestUser", followDate));
        _tracker.ReleaseFollowDispatch("uid-1");

        // Retry of the same follow event succeeds
        Assert.True(await _tracker.TryBeginFollowDispatchAsync("uid-1", "testuser", "TestUser", followDate));
        _tracker.ReleaseFollowDispatch("uid-1");

        // Genuine unfollow + re-follow with a later timestamp is suppressed
        Assert.False(await _tracker.TryBeginFollowDispatchAsync("uid-1", "testuser", "TestUser", followDate.AddMinutes(5)));
    }

    [Fact]
    public async Task TryBeginFollowDispatchAsync_EmptyUserId_ReturnsFalseWithoutPersisting()
    {
        var began = await _tracker.TryBeginFollowDispatchAsync("", "testuser", "TestUser", DateTime.UtcNow);

        Assert.False(began);
        Assert.Equal(0, await _tracker.GetPersistedFollowerCountAsync());
    }

    [Fact]
    public async Task SyncFollowersAsync_AddsNewUpdatesChangedNeverDeletesAndCoordinatesLiveFollows()
    {
        var unfollowedDate = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var existingDate = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.True(await _tracker.TryBeginFollowDispatchAsync("uid-unfollowed", "formerfollower", "FormerFollower", unfollowedDate));
        await _tracker.CompleteFollowDispatchAsync("uid-unfollowed");
        Assert.True(await _tracker.TryBeginFollowDispatchAsync("uid-existing", "oldhandle", "OldHandle", existingDate));
        await _tracker.CompleteFollowDispatchAsync("uid-existing");
        Assert.True(await _tracker.TryBeginFollowDispatchAsync("uid-unchanged", "sameuser", "SameUser", existingDate));
        await _tracker.CompleteFollowDispatchAsync("uid-unchanged");

        var historicalDate = DateTime.UtcNow.AddDays(-10);
        var liveFollowDate = DateTime.UtcNow;

        _twitchService.GetAllFollowersAsync(Arg.Any<IProgress<FollowerSyncProgress>?>(), Arg.Any<CancellationToken>())
            .Returns([
                new Follower { UserId = "uid-existing", Username = "newhandle", DisplayName = "NewHandle", FollowDate = DateTime.UtcNow },
                new Follower { UserId = "uid-unchanged", Username = "sameuser", DisplayName = "SameUser", FollowDate = DateTime.UtcNow },
                new Follower { UserId = "uid-historical", Username = "OldFollower", DisplayName = "OldFollower", FollowDate = historicalDate },
                new Follower { UserId = "uid-live", Username = "LiveFollower", DisplayName = "LiveFollower", FollowDate = liveFollowDate }
            ]);

        var stateChangedCount = 0;
        _tracker.SyncStateChanged += () => stateChangedCount++;

        var result = await _tracker.SyncFollowersAsync();

        Assert.Equal(2, stateChangedCount);
        Assert.Equal(4, result.TotalTwitchFollowers);
        Assert.Equal(2, result.AddedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.ExistingCount);
        Assert.Equal(5, result.TotalPersistedFollowers);
        Assert.Equal(5, await _tracker.GetPersistedFollowerCountAsync());
        Assert.Equal(result, _tracker.LastSyncResult);

        // Unfollowed viewer and historical synced follower are suppressed
        Assert.False(await _tracker.TryBeginFollowDispatchAsync("uid-unfollowed", "formerfollower", "FormerFollower", DateTime.UtcNow));
        Assert.False(await _tracker.TryBeginFollowDispatchAsync("uid-historical", "OldFollower", "OldFollower", DateTime.UtcNow));

        // Brand-new follower imported during the sync window still receives their matching first-follow notification
        Assert.True(await _tracker.TryBeginFollowDispatchAsync("uid-live", "LiveFollower", "LiveFollower", liveFollowDate));
        await _tracker.CompleteFollowDispatchAsync("uid-live");

        // Subsequent re-follow by that same user is suppressed
        Assert.False(await _tracker.TryBeginFollowDispatchAsync("uid-live", "LiveFollower", "LiveFollower", liveFollowDate.AddMinutes(2)));
    }

    [Fact]
    public async Task SyncFollowersAsync_WhenAlreadyInProgress_ThrowsInvalidOperationException()
    {
        var gate = new TaskCompletionSource<List<Follower>>();
        _twitchService.GetAllFollowersAsync(Arg.Any<IProgress<FollowerSyncProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(gate.Task);

        var firstSync = _tracker.SyncFollowersAsync();
        Assert.True(_tracker.IsSyncInProgress);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _tracker.SyncFollowersAsync());

        gate.SetResult([]);
        await firstSync;
        Assert.False(_tracker.IsSyncInProgress);
    }

    [Fact]
    public async Task CheckLocalTableFirstSetting_DefaultsToTrueAndPersistsChanges()
    {
        Assert.True(await _tracker.GetCheckLocalTableFirstAsync());

        await _tracker.SetCheckLocalTableFirstAsync(false);
        Assert.False(await _tracker.GetCheckLocalTableFirstAsync());

        // Verify persisted in DB for a fresh tracker instance
        var freshTracker = new FollowTracker(
            _logger,
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _twitchService);
        Assert.False(await freshTracker.GetCheckLocalTableFirstAsync());

        await freshTracker.SetCheckLocalTableFirstAsync(true);
        Assert.True(await freshTracker.GetCheckLocalTableFirstAsync());
    }

    [Fact]
    public async Task GetPersistedFollowerByUsernameAsync_NormalizesAndFindsFollower()
    {
        var followDate = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
        await _tracker.TryBeginFollowDispatchAsync("uid-1", "MixedCaseUser", "MixedCaseUser", followDate);

        var found = await _tracker.GetPersistedFollowerByUsernameAsync("MIXEDCASEUSER");
        Assert.NotNull(found);
        Assert.Equal("uid-1", found!.UserId);

        Assert.Null(await _tracker.GetPersistedFollowerByUsernameAsync("missinguser"));
    }

    [Fact]
    public async Task GetPersistedFollowersPageAsync_SupportsSearchSortingAndLinksViewerLastSeen()
    {
        var older = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastSeen = new DateTime(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc);

        await _tracker.TryBeginFollowDispatchAsync("uid-1", "alice", "Alice", older);
        await _tracker.TryBeginFollowDispatchAsync("uid-2", "bob", "Bob", newer);

        await using (var ctx = new ApplicationDbContext(_dbOptions))
        {
            ctx.Viewers.Add(new Viewer
            {
                UserId = "uid-1",
                Username = "alice",
                DisplayName = "Alice",
                LastSeen = lastSeen,
                isSub = true,
                isVip = true
            });
            await ctx.SaveChangesAsync();
        }

        var page = await _tracker.GetPersistedFollowersPageAsync(page: 0, pageSize: 10, sortBy: "followdate", sortDescending: true);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal("bob", page.Items[0].Username);
        Assert.Null(page.Items[0].ViewerId);
        Assert.Null(page.Items[0].LastSeen);

        Assert.Equal("alice", page.Items[1].Username);
        Assert.NotNull(page.Items[1].ViewerId);
        Assert.Equal(lastSeen, page.Items[1].LastSeen);
        Assert.True(page.Items[1].IsSub);
        Assert.True(page.Items[1].IsVip);

        var filtered = await _tracker.GetPersistedFollowersPageAsync(page: 0, pageSize: 10, search: "ALI");
        Assert.Equal(1, filtered.TotalCount);
        Assert.Single(filtered.Items);
        Assert.Equal("alice", filtered.Items[0].Username);
    }

    [Fact]
    public async Task SyncFollowersAsync_WithRemoveUnfollowed_RemovesUnfollowedButProtectsLiveFollowers()
    {
        var oldDate = DateTime.UtcNow.AddDays(-60);
        var liveDate = DateTime.UtcNow;

        Assert.True(await _tracker.TryBeginFollowDispatchAsync("uid-unfollowed", "oldunfollower", "OldUnfollower", oldDate));
        await _tracker.CompleteFollowDispatchAsync("uid-unfollowed");
        Assert.True(await _tracker.TryBeginFollowDispatchAsync("uid-kept", "stillfollowing", "StillFollowing", oldDate));
        await _tracker.CompleteFollowDispatchAsync("uid-kept");
        Assert.True(await _tracker.TryBeginFollowDispatchAsync("uid-live-recent", "justfollowed", "JustFollowed", liveDate));
        await _tracker.CompleteFollowDispatchAsync("uid-live-recent");

        _twitchService.GetAllFollowersAsync(Arg.Any<IProgress<FollowerSyncProgress>?>(), Arg.Any<CancellationToken>())
            .Returns([
                new Follower { UserId = "uid-kept", Username = "stillfollowing", DisplayName = "StillFollowing", FollowDate = oldDate },
                new Follower { UserId = "uid-new", Username = "brandnew", DisplayName = "BrandNew", FollowDate = oldDate }
            ]);

        var result = await _tracker.SyncFollowersAsync(removeUnfollowed: true);

        Assert.True(result.RemovedUnfollowed);
        Assert.Equal(1, result.AddedCount);
        Assert.Equal(1, result.RemovedCount);
        Assert.Equal(3, result.TotalPersistedFollowers);
        Assert.Null(await _tracker.GetPersistedFollowerByUsernameAsync("oldunfollower"));
        Assert.NotNull(await _tracker.GetPersistedFollowerByUsernameAsync("justfollowed"));
        Assert.NotNull(await _tracker.GetPersistedFollowerByUsernameAsync("brandnew"));
    }

    [Fact]
    public async Task ScheduledSync_PersistsConfigAndRunsOnlyWhenDue()
    {
        var baseTime = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        var defaultConfig = await _tracker.GetScheduleConfigAsync();
        Assert.False(defaultConfig.Enabled);
        Assert.Equal(30, defaultConfig.IntervalDays);

        // Disabled schedule does not run
        Assert.False(await _tracker.TryRunScheduledSyncIfDueAsync(baseTime));

        // Enable monthly full resync anchored at baseTime
        await _tracker.SaveScheduleConfigAsync(new FollowerSyncScheduleConfig(
            Enabled: true,
            IntervalDays: 30,
            RemoveUnfollowed: true,
            LastScheduledRunUtc: baseTime));

        _twitchService.GetAllFollowersAsync(Arg.Any<IProgress<FollowerSyncProgress>?>(), Arg.Any<CancellationToken>())
            .Returns([new Follower { UserId = "uid-1", Username = "alice", DisplayName = "Alice", FollowDate = baseTime.AddDays(-10) }]);

        // Not due after 15 days
        Assert.False(await _tracker.TryRunScheduledSyncIfDueAsync(baseTime.AddDays(15)));
        Assert.Equal(0, await _tracker.GetPersistedFollowerCountAsync());

        // Due after 30 days
        var runTime = baseTime.AddDays(30);
        Assert.True(await _tracker.TryRunScheduledSyncIfDueAsync(runTime));
        Assert.Equal(1, await _tracker.GetPersistedFollowerCountAsync());

        var updatedConfig = await _tracker.GetScheduleConfigAsync();
        Assert.Equal(runTime, updatedConfig.LastScheduledRunUtc);

        // Not due again immediately after running
        Assert.False(await _tracker.TryRunScheduledSyncIfDueAsync(runTime.AddHours(1)));
    }
}
