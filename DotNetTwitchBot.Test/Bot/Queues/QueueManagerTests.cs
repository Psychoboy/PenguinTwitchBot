using DotNetTwitchBot.Bot.Hubs;
using DotNetTwitchBot.Bot.Models.Actions;
using DotNetTwitchBot.Bot.Models.Queues;
using DotNetTwitchBot.Bot.Queues;
using DotNetTwitchBot.Repository;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotNetTwitchBot.Test.Bot.Queues
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

            var queueManager = new QueueManager(logger, scopeFactory, loggerFactory, executionLogger, hubContext);

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
            loggerFactory.CreateLogger<ActionQueue>().Returns(Substitute.For<ILogger<ActionQueue>>());

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
            var queueManager = new QueueManager(logger, scopeFactory, loggerFactory, executionLogger, hubContext);
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
            loggerFactory.CreateLogger<ActionQueue>().Returns(Substitute.For<ILogger<ActionQueue>>());

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
            var queueManager = new QueueManager(logger, scopeFactory, loggerFactory, executionLogger, hubContext);
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

            var queueManager = new QueueManager(logger, scopeFactory, loggerFactory, executionLogger, hubContext);

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

            var queueManager = new QueueManager(logger, scopeFactory, loggerFactory, executionLogger, hubContext);

            var config = new QueueConfiguration
            {
                Name = "Default",
                IsBlocking = false
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await queueManager.UpdateQueueAsync(config));
        }
    }
}
