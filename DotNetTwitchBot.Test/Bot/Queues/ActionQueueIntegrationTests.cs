using DotNetTwitchBot.Bot.Actions;
using DotNetTwitchBot.Bot.Models.Actions;
using DotNetTwitchBot.Bot.Models.Actions.SubActions;
using DotNetTwitchBot.Bot.Models.Queues;
using DotNetTwitchBot.Bot.Queues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ActionService = DotNetTwitchBot.Bot.Actions.Action;

namespace DotNetTwitchBot.Test.Bot.Queues
{
    public class ActionQueueIntegrationTests
    {
        [Fact]
        public async Task EnqueueAction_CreatesLogEntryWithPendingState()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionQueue>>();
            var executionLoggerLogger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var executionLogger = new ActionExecutionLogger(executionLoggerLogger);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddScoped<ActionService>();
            serviceCollection.AddTransient<DotNetTwitchBot.Bot.Actions.SubActions.SubActionHandlerFactory>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            var queue = new ActionQueue(
                "test-queue",
                isBlocking: true,
                maxConcurrentActions: 1,
                logger,
                scopeFactory,
                executionLogger);

            var action = new ActionType
            {
                Name = "TestAction",
                QueueName = "test-queue",
                SubActions = []
            };

            var variables = new Dictionary<string, string> { { "test", "value" } };

            // Act
            await queue.EnqueueAsync(action, variables);

            // Assert
            var logs = executionLogger.GetRecentLogs();
            Assert.Single(logs);

            var log = logs[0];
            Assert.Equal("TestAction", log.ActionName);
            Assert.Equal("test-queue", log.QueueName);
            Assert.Equal(ActionExecutionState.Pending, log.State);
            Assert.Contains("test", log.Variables.Keys);
            Assert.Equal("value", log.Variables["test"]);
            Assert.Null(log.StartedAt);
            Assert.Null(log.CompletedAt);
        }

        [Fact]
        public async Task ExecuteAction_UpdatesLogToCompletedOrFailed()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionQueue>>();
            var executionLoggerLogger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var executionLogger = new ActionExecutionLogger(executionLoggerLogger);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddScoped<ActionService>();
            serviceCollection.AddTransient<DotNetTwitchBot.Bot.Actions.SubActions.SubActionHandlerFactory>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            var queue = new ActionQueue(
                "test-queue",
                isBlocking: true,
                maxConcurrentActions: 1,
                logger,
                scopeFactory,
                executionLogger);

            var action = new ActionType
            {
                Name = "TestAction",
                QueueName = "test-queue",
                SubActions = []
            };

            var variables = new Dictionary<string, string>();

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
    }
}
