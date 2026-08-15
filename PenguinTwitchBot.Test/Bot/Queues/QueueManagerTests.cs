using PenguinTwitchBot.Bot.Hubs;
using PenguinTwitchBot.Database.Bot.Models.Queues;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Bot.WebSocketEvents;
using PenguinTwitchBot.Database.Repository;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Queues
{
    public class QueueManagerTests
    {
        [Fact]
        public async Task CreateQueueAsync_ThrowsException_WhenQueueNameIsDefault()
        {
            // Arrange
            var logger = Substitute.For<ILogger<QueueManager>>();
            var loggerFactory = Substitute.For<ILoggerFactory>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var executionLogger = Substitute.For<IActionExecutionLogger>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var wsEventHandler = Substitute.For<IWsEventHandler>();

            var queueManager = new QueueManager(logger, scopeFactory, loggerFactory, executionLogger, wsEventHandler, hubContext, new GlobalConcurrencyLimiter());

            var config = new QueueConfiguration
            {
                Name = "Default",
                IsBlocking = true
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await queueManager.CreateQueueAsync(config));
        }

        [Fact]
        public async Task GetQueueAsync_ReturnsDefaultQueue_WhenQueueNotFound()
        {
            // Arrange
            var logger = Substitute.For<ILogger<QueueManager>>();
            var loggerFactory = Substitute.For<ILoggerFactory>();
#pragma warning disable NS1000 // Non-virtual setup specification.
            loggerFactory.CreateLogger<ActionQueue>().Returns(Substitute.For<ILogger<ActionQueue>>());
#pragma warning restore NS1000 // Non-virtual setup specification.

            var db = Substitute.For<IUnitOfWork>();
            var queueRepo = Substitute.For<IQueueConfigurationsRepository>();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(db);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            db.QueueConfigurations.Returns(queueRepo);
            queueRepo.GetAllAsync().Returns(new List<QueueConfiguration>());

            var executionLogger = Substitute.For<IActionExecutionLogger>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var wsEventHandler = Substitute.For<IWsEventHandler>();
            var queueManager = new QueueManager(logger, scopeFactory, loggerFactory, executionLogger, wsEventHandler, hubContext, new GlobalConcurrencyLimiter());
            await queueManager.StartAsync(CancellationToken.None);

            // Act
            var queue = await queueManager.GetQueueAsync("nonexistent-queue");

            // Assert
            Assert.NotNull(queue);
            Assert.Equal("Default", queue.Name);
            Assert.False(queue.IsBlocking);
        }

        [Fact]
        public async Task GetQueueStatisticsAsync_ReturnsCorrectStatistics()
        {
            // Arrange
            var logger = Substitute.For<ILogger<QueueManager>>();
            var loggerFactory = Substitute.For<ILoggerFactory>();
#pragma warning disable NS1000 // Non-virtual setup specification.
            loggerFactory.CreateLogger<ActionQueue>().Returns(Substitute.For<ILogger<ActionQueue>>());
#pragma warning restore NS1000 // Non-virtual setup specification.

            var db = Substitute.For<IUnitOfWork>();
            var queueRepo = Substitute.For<IQueueConfigurationsRepository>();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(db);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            db.QueueConfigurations.Returns(queueRepo);
            queueRepo.GetAllAsync().Returns(new List<QueueConfiguration>());

            var executionLogger = Substitute.For<IActionExecutionLogger>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var wsEventHandler = Substitute.For<IWsEventHandler>();
            var queueManager = new QueueManager(logger, scopeFactory, loggerFactory, executionLogger, wsEventHandler, hubContext, new GlobalConcurrencyLimiter());
            await queueManager.StartAsync(CancellationToken.None);

            // Act
            var stats = await queueManager.GetQueueStatisticsAsync("Default");

            // Assert
            Assert.NotNull(stats);
            Assert.Equal("Default", stats.QueueName);
            Assert.False(stats.IsBlocking);
            Assert.Equal(0, stats.PendingActions);
            Assert.Equal(0, stats.CompletedActions);
        }

        [Fact]
        public async Task DeleteQueueAsync_ThrowsException_WhenDeletingDefaultQueue()
        {
            // Arrange
            var logger = Substitute.For<ILogger<QueueManager>>();
            var loggerFactory = Substitute.For<ILoggerFactory>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var executionLogger = Substitute.For<IActionExecutionLogger>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var wsEventHandler = Substitute.For<IWsEventHandler>();

            var queueManager = new QueueManager(logger, scopeFactory, loggerFactory, executionLogger, wsEventHandler, hubContext, new GlobalConcurrencyLimiter());

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await queueManager.DeleteQueueAsync("Default"));
        }

        [Fact]
        public async Task UpdateQueueAsync_ThrowsException_WhenUpdatingDefaultQueue()
        {
            // Arrange
            var logger = Substitute.For<ILogger<QueueManager>>();
            var loggerFactory = Substitute.For<ILoggerFactory>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var executionLogger = Substitute.For<IActionExecutionLogger>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var wsEventHandler = Substitute.For<IWsEventHandler>();

            var queueManager = new QueueManager(logger, scopeFactory, loggerFactory, executionLogger, wsEventHandler, hubContext, new GlobalConcurrencyLimiter());

            var config = new QueueConfiguration
            {
                Name = "Default",
                IsBlocking = false
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await queueManager.UpdateQueueAsync(config));
        }

        [Fact]
        public async Task ClearQueueAsync_ThrowsException_WhenQueueNotFound()
        {
            // Arrange
            var logger = Substitute.For<ILogger<QueueManager>>();
            var loggerFactory = Substitute.For<ILoggerFactory>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var executionLogger = Substitute.For<IActionExecutionLogger>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var wsEventHandler = Substitute.For<IWsEventHandler>();

            var queueManager = new QueueManager(logger, scopeFactory, loggerFactory, executionLogger, wsEventHandler, hubContext, new GlobalConcurrencyLimiter());

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await queueManager.ClearQueueAsync("nonexistent-queue"));
        }

        [Fact]
        public async Task ClearQueueAsync_ClearsPendingActions_ForExistingQueue()
        {
            // Arrange
            var logger = Substitute.For<ILogger<QueueManager>>();
            var loggerFactory = Substitute.For<ILoggerFactory>();
#pragma warning disable NS1000 // Non-virtual setup specification.
            loggerFactory.CreateLogger<ActionQueue>().Returns(Substitute.For<ILogger<ActionQueue>>());
#pragma warning restore NS1000 // Non-virtual setup specification.

            var executionLogger = new ActionExecutionLogger(Substitute.For<ILogger<ActionExecutionLogger>>(), Substitute.For<IHubContext<MainHub>>());
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var wsEventHandler = Substitute.For<IWsEventHandler>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var queueManager = new QueueManager(logger, scopeFactory, loggerFactory, executionLogger, wsEventHandler, hubContext, new GlobalConcurrencyLimiter());

            // Intentionally do not call StartAsync: GetQueueAsync will create a fallback
            // Default queue without starting its processing loop, so enqueued actions stay
            // pending and aren't raced away by real execution.
            var queue = await queueManager.GetQueueAsync("Default");
            var variables = new ConcurrentDictionary<string, string>();
            await queue.EnqueueAsync(new ActionType { Name = "Action1", QueueName = "Default", SubActions = [] }, variables);
            await queue.EnqueueAsync(new ActionType { Name = "Action2", QueueName = "Default", SubActions = [] }, variables);

            // Act
            var clearedCount = await queueManager.ClearQueueAsync("Default");

            // Assert
            Assert.Equal(2, clearedCount);
            var stats = await queueManager.GetQueueStatisticsAsync("Default");
            Assert.Equal(0, stats.PendingActions);
        }
    }
}
