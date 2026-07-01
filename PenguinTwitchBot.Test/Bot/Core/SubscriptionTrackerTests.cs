using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;
using Xunit;

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
}
