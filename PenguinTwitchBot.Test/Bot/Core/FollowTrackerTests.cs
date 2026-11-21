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
    public async Task TryRecordFollowAsync_FirstFollow_PersistsAndReturnsTrue()
    {
        var followDate = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

        var isNew = await _tracker.TryRecordFollowAsync("uid-1", "TestUser", "TestUser", followDate);

        Assert.True(isNew);
        Assert.Equal(1, await _tracker.GetPersistedFollowerCountAsync());

        await using var verifyContext = new ApplicationDbContext(_dbOptions);
        var saved = await verifyContext.Followers.SingleAsync();
        Assert.Equal("uid-1", saved.UserId);
        Assert.Equal("testuser", saved.Username);
        Assert.Equal("TestUser", saved.DisplayName);
        Assert.Equal(followDate, saved.FollowDate);
    }

    [Fact]
    public async Task TryRecordFollowAsync_Refollow_ReturnsFalsePreservesOriginalDateAndUpdatesName()
    {
        var originalDate = new DateTime(2025, 1, 15, 8, 30, 0, DateTimeKind.Utc);
        var refollowDate = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(await _tracker.TryRecordFollowAsync("uid-1", "oldname", "OldName", originalDate));
        var isNewOnRefollow = await _tracker.TryRecordFollowAsync("uid-1", "NewName", "NewName", refollowDate);

        Assert.False(isNewOnRefollow);
        Assert.Equal(1, await _tracker.GetPersistedFollowerCountAsync());

        await using var verifyContext = new ApplicationDbContext(_dbOptions);
        var saved = await verifyContext.Followers.SingleAsync();
        Assert.Equal("newname", saved.Username);
        Assert.Equal("NewName", saved.DisplayName);
        Assert.Equal(originalDate, saved.FollowDate);
    }

    [Fact]
    public async Task TryRecordFollowAsync_EmptyUserId_ReturnsFalseWithoutPersisting()
    {
        var isNew = await _tracker.TryRecordFollowAsync("", "testuser", "TestUser", DateTime.UtcNow);

        Assert.False(isNew);
        Assert.Equal(0, await _tracker.GetPersistedFollowerCountAsync());
    }

    [Fact]
    public async Task SyncFollowersAsync_AddsNewUpdatesChangedAndNeverDeletesUnfollowed()
    {
        var unfollowedDate = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var existingDate = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        await _tracker.TryRecordFollowAsync("uid-unfollowed", "formerfollower", "FormerFollower", unfollowedDate);
        await _tracker.TryRecordFollowAsync("uid-existing", "oldhandle", "OldHandle", existingDate);
        await _tracker.TryRecordFollowAsync("uid-unchanged", "sameuser", "SameUser", existingDate);

        _twitchService.GetAllFollowersAsync(Arg.Any<IProgress<FollowerSyncProgress>?>(), Arg.Any<CancellationToken>())
            .Returns([
                new Follower { UserId = "uid-existing", Username = "newhandle", DisplayName = "NewHandle", FollowDate = DateTime.UtcNow },
                new Follower { UserId = "uid-unchanged", Username = "sameuser", DisplayName = "SameUser", FollowDate = DateTime.UtcNow },
                new Follower { UserId = "uid-new", Username = "BrandNew", DisplayName = "BrandNew", FollowDate = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc) }
            ]);

        var result = await _tracker.SyncFollowersAsync();

        Assert.Equal(3, result.TotalTwitchFollowers);
        Assert.Equal(1, result.AddedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.ExistingCount);
        Assert.Equal(4, result.TotalPersistedFollowers);
        Assert.Equal(4, await _tracker.GetPersistedFollowerCountAsync());
        Assert.Equal(result, _tracker.LastSyncResult);

        // Unfollowed viewer is still persisted so re-following will not re-trigger events
        Assert.False(await _tracker.TryRecordFollowAsync("uid-unfollowed", "formerfollower", "FormerFollower", DateTime.UtcNow));
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
}
