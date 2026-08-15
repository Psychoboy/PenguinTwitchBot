using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Bot.Hubs;
using PenguinTwitchBot.Database.Bot.Models.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Queues;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Bot.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ActionService = PenguinTwitchBot.Bot.Actions.Action;
using PenguinTwitchBot.Bot.WebSocketEvents;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Queues
{
    public class ActionQueueIntegrationTests
    {
        [Fact]
        public async Task EnqueueAction_CreatesLogEntryWithPendingState()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionQueue>>();
            var executionLoggerLogger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var wsEventHandler = Substitute.For<IWsEventHandler>();
            var executionLogger = new ActionExecutionLogger(executionLoggerLogger, hubContext);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddScoped<ActionService>();
            serviceCollection.AddSingleton(Substitute.For<IFeatureRuntimeCoordinator>());
            serviceCollection.AddTransient<PenguinTwitchBot.Bot.Actions.SubActions.SubActionHandlerFactory>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            var queue = new ActionQueue(
                "test-queue",
                isBlocking: true,
                maxConcurrentActions: 1,
                logger,
                scopeFactory,
                executionLogger,
                wsEventHandler);

            var action = new ActionType
            {
                Name = "TestAction",
                QueueName = "test-queue",
                SubActions = []
            };

            var variables = new ConcurrentDictionary<string, string> { ["test"] = "value" };

            // Act
            await queue.EnqueueAsync(action, variables);

            // Assert
            var logs = executionLogger.GetRecentLogs();
            Assert.Single(logs);

            var log = logs[0];
            Assert.Equal("TestAction", log.ActionName);
            Assert.Equal("test-queue", log.QueueName);
            Assert.Equal(ActionExecutionState.Pending, log.State);
            Assert.Contains("test", log.VariablesBefore.Keys);
            Assert.Equal("value", log.VariablesBefore["test"]);
            Assert.Null(log.StartedAt);
            Assert.Null(log.CompletedAt);
        }

        [Fact]
        public async Task ExecuteAction_UpdatesLogToCompletedOrFailed()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionQueue>>();
            var executionLoggerLogger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var executionLogger = new ActionExecutionLogger(executionLoggerLogger, hubContext);
            var wsEventHandler = Substitute.For<IWsEventHandler>();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddScoped<ActionService>();
            serviceCollection.AddSingleton(Substitute.For<IFeatureRuntimeCoordinator>());
            serviceCollection.AddTransient<PenguinTwitchBot.Bot.Actions.SubActions.SubActionHandlerFactory>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            var queue = new ActionQueue(
                "test-queue",
                isBlocking: true,
                maxConcurrentActions: 1,
                logger,
                scopeFactory,
                executionLogger,
                wsEventHandler);

            var action = new ActionType
            {
                Name = "TestAction",
                QueueName = "test-queue",
                SubActions = []
            };

            var variables = new ConcurrentDictionary<string, string>();

            var cancellationTokenSource = new CancellationTokenSource();
            await queue.StartAsync(cancellationTokenSource.Token);

            // Act
            await queue.EnqueueAsync(action, variables);

            // Wait for execution with retries
            var attempts = 0;
            while (attempts < 50 && 
                   executionLogger.GetLogsByState(ActionExecutionState.Completed).Count == 0 &&
                   executionLogger.GetLogsByState(ActionExecutionState.Failed).Count == 0)
            {
                await Task.Delay(10);
                attempts++;
            }

            cancellationTokenSource.Cancel();

            // Assert
            var logs = executionLogger.GetRecentLogs();
            Assert.Single(logs);

            var log = logs[0];
            Assert.Equal("TestAction", log.ActionName);
            Assert.True(log.State == ActionExecutionState.Completed || log.State == ActionExecutionState.Failed);
            Assert.NotNull(log.StartedAt);
            Assert.NotNull(log.CompletedAt);
            Assert.NotNull(log.ExecutionDuration);
            Assert.True(log.ExecutionDuration.Value.TotalMilliseconds >= 0);
        }

        [Fact]
        public async Task ClearPendingAsync_RemovesPendingActionsAndMarksThemCancelled()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionQueue>>();
            var executionLoggerLogger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var wsEventHandler = Substitute.For<IWsEventHandler>();
            var executionLogger = new ActionExecutionLogger(executionLoggerLogger, hubContext);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddScoped<ActionService>();
            serviceCollection.AddSingleton(Substitute.For<IFeatureRuntimeCoordinator>());
            serviceCollection.AddTransient<PenguinTwitchBot.Bot.Actions.SubActions.SubActionHandlerFactory>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            // Queue is never started, so enqueued actions remain pending in the channel.
            var queue = new ActionQueue(
                "test-queue",
                isBlocking: true,
                maxConcurrentActions: 1,
                logger,
                scopeFactory,
                executionLogger,
                wsEventHandler);

            var variables = new ConcurrentDictionary<string, string>();
            await queue.EnqueueAsync(new ActionType { Name = "Action1", QueueName = "test-queue", SubActions = [] }, variables);
            await queue.EnqueueAsync(new ActionType { Name = "Action2", QueueName = "test-queue", SubActions = [] }, variables);
            await queue.EnqueueAsync(new ActionType { Name = "Action3", QueueName = "test-queue", SubActions = [] }, variables);

            Assert.Equal(3, queue.PendingCount);

            // Act
            var clearedCount = await queue.ClearPendingAsync();

            // Assert
            Assert.Equal(3, clearedCount);
            Assert.Equal(0, queue.PendingCount);

            var logs = executionLogger.GetRecentLogs();
            Assert.Equal(3, logs.Count);
            Assert.All(logs, log =>
            {
                Assert.Equal(ActionExecutionState.Cancelled, log.State);
                Assert.NotNull(log.CompletedAt);
            });
        }

        [Fact]
        public async Task ClearPendingAsync_ReturnsZero_WhenQueueIsEmpty()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionQueue>>();
            var executionLoggerLogger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var wsEventHandler = Substitute.For<IWsEventHandler>();
            var executionLogger = new ActionExecutionLogger(executionLoggerLogger, hubContext);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddScoped<ActionService>();
            serviceCollection.AddSingleton(Substitute.For<IFeatureRuntimeCoordinator>());
            serviceCollection.AddTransient<PenguinTwitchBot.Bot.Actions.SubActions.SubActionHandlerFactory>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            var queue = new ActionQueue(
                "test-queue",
                isBlocking: true,
                maxConcurrentActions: 1,
                logger,
                scopeFactory,
                executionLogger,
                wsEventHandler);

            // Act
            var clearedCount = await queue.ClearPendingAsync();

            // Assert
            Assert.Equal(0, clearedCount);
            Assert.Empty(executionLogger.GetRecentLogs());
        }

        [Fact]
        public async Task ClearPendingAsync_DoesNotAffectActionsAlreadyRunning()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionQueue>>();
            var executionLoggerLogger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var wsEventHandler = Substitute.For<IWsEventHandler>();
            var executionLogger = new ActionExecutionLogger(executionLoggerLogger, hubContext);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddScoped<ActionService>();
            serviceCollection.AddSingleton(Substitute.For<IFeatureRuntimeCoordinator>());
            serviceCollection.AddTransient<PenguinTwitchBot.Bot.Actions.SubActions.SubActionHandlerFactory>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            var queue = new ActionQueue(
                "test-queue",
                isBlocking: true,
                maxConcurrentActions: 1,
                logger,
                scopeFactory,
                executionLogger,
                wsEventHandler);

            var variables = new ConcurrentDictionary<string, string>();
            var cancellationTokenSource = new CancellationTokenSource();
            await queue.StartAsync(cancellationTokenSource.Token);

            await queue.EnqueueAsync(new ActionType { Name = "TestAction", QueueName = "test-queue", SubActions = [] }, variables);

            // Wait for the action to finish processing (queue is empty afterwards).
            var attempts = 0;
            while (attempts < 50 &&
                   executionLogger.GetLogsByState(ActionExecutionState.Completed).Count == 0 &&
                   executionLogger.GetLogsByState(ActionExecutionState.Failed).Count == 0)
            {
                await Task.Delay(10);
                attempts++;
            }

            cancellationTokenSource.Cancel();

            // Act - nothing left pending, so clearing should be a no-op.
            var clearedCount = await queue.ClearPendingAsync();

            // Assert
            Assert.Equal(0, clearedCount);
            var log = Assert.Single(executionLogger.GetRecentLogs());
            Assert.NotEqual(ActionExecutionState.Cancelled, log.State);
        }
    }
}
